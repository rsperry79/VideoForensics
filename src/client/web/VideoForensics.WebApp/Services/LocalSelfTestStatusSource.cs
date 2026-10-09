using System.Reactive.Linq;
using System.Reactive.Subjects;

using Microsoft.Extensions.Logging;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Ui.Shared.Contracts;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// <see cref="ISelfTestStatusSource"/> for the WebApp, which runs self-tests in-process and has no realtime hub
    /// to itself. It samples <see cref="IRingSelfTestService"/> on a fixed interval and pushes a status only when it
    /// differs from the last one, using the same <see cref="ISelfTestStatusChangeDetector"/> the admin broadcast uses.
    /// This replaces the page's former status polling timer.
    /// <para>
    /// Scoped per circuit, because <see cref="IRingSelfTestService"/> is scoped. Sampling starts on the first
    /// subscription and stops on <see cref="Dispose"/>, which the scope's disposal calls.
    /// </para>
    /// <para>
    /// This is a second sampler next to <c>SelfTestStatusBroadcastService</c>, which pushes to the admin hub group.
    /// It is kept per circuit: the broadcast runs for the whole server and does not feed in-process subscribers.
    /// </para>
    /// </summary>
    public sealed class LocalSelfTestStatusSource : ISelfTestStatusSource, IDisposable
    {
        /// <summary>Default time between samples, matching the admin broadcast's cadence.</summary>
        public static readonly TimeSpan DefaultSampleInterval = TimeSpan.FromMilliseconds(1000);

        private readonly IRingSelfTestService _selfTestService;
        private readonly ISelfTestStatusChangeDetector _changeDetector;
        private readonly ILogger<LocalSelfTestStatusSource> _logger;
        private readonly TimeSpan _sampleInterval;
        private readonly ReplaySubject<SelfTestStatusDto> _statuses = new(1);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly object _gate = new();
        private SelfTestStatusDto? _lastState;
        private bool _started;
        private bool _disposed;

        /// <summary>
        /// Creates the source over the circuit's self-test service.
        /// </summary>
        /// <param name="selfTestService">The service sampled for status.</param>
        /// <param name="changeDetector">Decides whether a sample differs from the last emitted status.</param>
        /// <param name="logger">Receives an error when a sample fails. A failed sample is skipped, not rethrown.</param>
        /// <param name="sampleInterval">Time between samples; defaults to <see cref="DefaultSampleInterval"/>.</param>
        public LocalSelfTestStatusSource(
            IRingSelfTestService selfTestService,
            ISelfTestStatusChangeDetector changeDetector,
            ILogger<LocalSelfTestStatusSource> logger,
            TimeSpan? sampleInterval = null)
        {
            _selfTestService = selfTestService;
            _changeDetector = changeDetector;
            _logger = logger;
            _sampleInterval = sampleInterval ?? DefaultSampleInterval;
        }

        /// <inheritdoc />
        public IObservable<SelfTestStatusDto> Status
        {
            get
            {
                return Observable.Defer(() =>
                {
                    EnsureSampling();
                    return _statuses.AsObservable();
                });
            }
        }

        /// <summary>
        /// Takes one sample and emits it if it differs from the last emitted status. The sampling loop calls this on
        /// each tick. It is public so a caller can drive sampling deterministically.
        /// </summary>
        public async Task SampleOnceAsync(CancellationToken cancellationToken)
        {
            SelfTestStatusDto current;
            try
            {
                current = await _selfTestService.GetStatusAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Self-test status sample failed; skipping this tick");
                return;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (!_changeDetector.HasChanged(_lastState, current))
                {
                    return;
                }

                _lastState = current;
                _statuses.OnNext(current);
            }
        }

        /// <summary>Stops sampling and completes subscribers.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _statuses.OnCompleted();
            }

            _cancellation.Cancel();
            _cancellation.Dispose();
        }

        private void EnsureSampling()
        {
            lock (_gate)
            {
                if (_started || _disposed)
                {
                    return;
                }

                _started = true;
                // Capture the token now: the source may be disposed before the task body first runs.
                CancellationToken token = _cancellation.Token;
                _ = Task.Run(() => RunAsync(token));
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(_sampleInterval);
            try
            {
                // Sample immediately so a page that opens during a run does not wait a full interval for its first value.
                do
                {
                    await SampleOnceAsync(cancellationToken);
                }
                while (await timer.WaitForNextTickAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                // Dispose stopped the loop. This is the normal shutdown path.
            }
        }
    }
}
