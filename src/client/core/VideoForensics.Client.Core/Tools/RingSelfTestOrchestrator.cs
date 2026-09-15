using Microsoft.Extensions.Logging;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Core.Contracts;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Client.Core.Tools
{
    /// <summary>
    /// Orchestrates Ring self-test runs, managing their lifecycle and state. Ensures only one
    /// run executes at a time (EndpointRegistry's ambient state is not thread-safe for concurrent runs).
    /// </summary>
    public class RingSelfTestOrchestrator : IRingSelfTestOrchestrator
    {
        private readonly ILogger<RingSelfTestOrchestrator> _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly object _lock = new();

        private SelfTestRunStatus _status = SelfTestRunStatus.Idle;
        private DateTime? _startedAtUtc;
        private DateTime? _completedAtUtc;
        private string? _error;
        private IndexDocument? _lastResult;

        public RingSelfTestOrchestrator(
            ILogger<RingSelfTestOrchestrator> logger,
            ISessionProvider sessionProvider)
        {
            _logger = logger;
            _sessionProvider = sessionProvider;
        }

        /// <summary>
        /// Attempts to start a self-test run. Returns false if a run is already in progress.
        /// On success, fires the run asynchronously via Task.Run (fire-and-forget from the caller's perspective).
        /// </summary>
        public bool TryStartRun(RunOptions options, string outputDir)
        {
            lock (_lock)
            {
                if (_status == SelfTestRunStatus.Running)
                {
                    return false;
                }

                _status = SelfTestRunStatus.Running;
                _startedAtUtc = DateTime.UtcNow;
                _completedAtUtc = null;
                _error = null;
                _lastResult = null;

                _ = Task.Run(() => ExecuteRunAsync(options, outputDir));
                return true;
            }
        }

        /// <summary>
        /// Executes the self-test run asynchronously, updating state and logging errors.
        /// </summary>
        private async Task ExecuteRunAsync(RunOptions options, string outputDir)
        {
            try
            {
                Session? session = _sessionProvider.GetSession();
                if (session == null)
                {
                    lock (_lock)
                    {
                        _status = SelfTestRunStatus.Failed;
                        _error = "No active Ring session";
                        _completedAtUtc = DateTime.UtcNow;
                    }

                    _logger.LogError("Cannot start self-test run: no active Ring session");
                    return;
                }

                var runner = new Runner(session, outputDir, quiet: true);
                IndexDocument result = await runner.RunAsync(options, credentialSource: "Database");

                lock (_lock)
                {
                    _lastResult = result;
                    _status = SelfTestRunStatus.Completed;
                    _completedAtUtc = DateTime.UtcNow;
                }

                _logger.LogInformation(
                    "Self-test run completed: {SucceededCount}/{TotalCount} calls succeeded",
                    result.Summary.Succeeded,
                    result.Summary.TotalCalls);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _status = SelfTestRunStatus.Failed;
                    _error = ex.Message;
                    _completedAtUtc = DateTime.UtcNow;
                }

                _logger.LogError(ex, "Self-test run failed");
            }
        }

        /// <summary>
        /// Gets the current run status and timing information.
        /// </summary>
        public (SelfTestRunStatus Status, DateTime? StartedAtUtc, DateTime? CompletedAtUtc, string? Error) GetStatus()
        {
            lock (_lock)
            {
                return (_status, _startedAtUtc, _completedAtUtc, _error);
            }
        }

        /// <summary>
        /// Gets the result of the last completed run, or null if no run has completed yet.
        /// </summary>
        public IndexDocument? GetResult()
        {
            lock (_lock)
            {
                return _lastResult;
            }
        }
    }
}
