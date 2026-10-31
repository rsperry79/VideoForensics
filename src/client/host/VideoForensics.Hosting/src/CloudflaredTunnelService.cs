using CloudflaredKit;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Hosting
{
    public enum TunnelKind
    {
        None,
        Named
    }

    public enum TunnelStatus
    {
        Stopped,
        Starting,
        Running,
        Failed
    }

    public sealed record TunnelState(
        TunnelKind Kind,
        TunnelStatus Status,
        string? PublicUrl,
        string? ErrorMessage,
        IReadOnlyList<string> RecentLogLines);

    public interface ICloudflaredTunnelService
    {
        /// <summary>Starts the named tunnel. <paramref name="publicUrl"/> is the saved Internet address that routes to it, reported in the state.</summary>
        Task StartPermanentTunnelAsync(string tunnelToken, CancellationToken ct, string? publicUrl = null);
        Task StopAsync(CancellationToken ct);
        TunnelState GetState();
    }

    public sealed class CloudflaredTunnelService : ICloudflaredTunnelService, IAsyncDisposable
    {
        private static readonly TimeSpan DisposeStopTimeout = TimeSpan.FromSeconds(5);

        private readonly ICloudflaredService _cloudflared;
        private readonly IOptionsMonitorCache<CloudflaredOptions> _optionsCache;
        private readonly ILogger<CloudflaredTunnelService> _logger;
        private readonly IChildProcessReaper _childProcessReaper;
        private readonly object _gate = new();
        private volatile bool _disposing;
        private TunnelState _state = new(
            TunnelKind.None,
            TunnelStatus.Stopped,
            null,
            null,
            Array.Empty<string>());

        public CloudflaredTunnelService(
            ICloudflaredService cloudflared,
            IOptionsMonitorCache<CloudflaredOptions> optionsCache,
            ILogger<CloudflaredTunnelService>? logger = null,
            IChildProcessReaper? childProcessReaper = null)
        {
            _cloudflared = cloudflared;
            _optionsCache = optionsCache;
            _logger = logger ?? NullLogger<CloudflaredTunnelService>.Instance;
            _childProcessReaper = childProcessReaper ?? new NoOpChildProcessReaper();

            // CloudflaredKit reports its own process dying only as a warning. A dead tunnel cuts off remote access, so it is
            // logged here at Error, with the exit code.
            _cloudflared.TunnelExitedUnexpectedly += OnTunnelExitedUnexpectedly;
        }

        public async Task StartPermanentTunnelAsync(string tunnelToken, CancellationToken ct, string? publicUrl = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tunnelToken);

            lock (_gate)
            {
                if (_state.Status is TunnelStatus.Starting or TunnelStatus.Running)
                {
                    return;
                }

                _state = _state with
                {
                    Kind = TunnelKind.Named,
                    Status = TunnelStatus.Starting,
                    ErrorMessage = null
                };
            }

            try
            {
                _ = _optionsCache.TryRemove(Options.DefaultName);
                _ = _optionsCache.TryAdd(Options.DefaultName, new CloudflaredOptions
                {
                    TunnelToken = tunnelToken
                });

                _ = await _cloudflared.StartAsync(ct);

                // cloudflared outlives a service that exits without stopping it (an update restart, or a crash). Orphans each hold
                // four connections and Cloudflare caps a tunnel at 25 replicas, so enough of them block every new start.
                _ = _childProcessReaper.AdoptChildren("cloudflared.exe");
                _logger.LogInformation("Cloudflare tunnel started");
                lock (_gate)
                {
                    _state = _state with { Status = TunnelStatus.Running, PublicUrl = publicUrl };
                }
            }
            catch (OperationCanceledException)
            {
                // A cancelled start leaves no tunnel running, so it returns to Stopped rather than sticking in Starting.
                lock (_gate)
                {
                    _state = _state with { Kind = TunnelKind.None, Status = TunnelStatus.Stopped };
                }

                throw;
            }
            catch (Exception ex)
            {
                // Every failure is logged and shown with its reason, so the page never reports a tunnel that did not start.
                _logger.LogError(ex, "Cloudflare tunnel failed to start");
                lock (_gate)
                {
                    _state = _state with
                    {
                        Status = TunnelStatus.Failed,
                        ErrorMessage = $"Could not start the Cloudflare tunnel: {ex.Message}"
                    };
                }

                throw;
            }
        }

        private void OnTunnelExitedUnexpectedly(int exitCode)
        {
            // Shutdown stops the tunnel on purpose, so its exit is not a failure to report.
            if (_disposing)
            {
                return;
            }

            _logger.LogError("Cloudflare tunnel exited unexpectedly (exit code {ExitCode}); remote access through it is down", exitCode);

            // Only a tunnel that was up or starting can have died. Without this the page would keep saying it is running.
            lock (_gate)
            {
                if (_state.Status is TunnelStatus.Starting or TunnelStatus.Running)
                {
                    _state = _state with
                    {
                        Status = TunnelStatus.Failed,
                        ErrorMessage = $"The Cloudflare tunnel stopped unexpectedly (exit code {exitCode})."
                    };
                }
            }
        }

        public async Task StopAsync(CancellationToken ct)
        {
            await _cloudflared.StopAsync(ct);
            lock (_gate)
            {
                _state = _state with
                {
                    Kind = TunnelKind.None,
                    Status = TunnelStatus.Stopped,
                    PublicUrl = null,
                    ErrorMessage = null
                };
            }
        }

        /// <summary>
        /// Stops cloudflared when the service shuts down, so it closes its Cloudflare connections cleanly instead of being killed
        /// with the process. The child process reaper still ends it if this stop fails or times out.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposing)
            {
                return;
            }

            _disposing = true;
            _cloudflared.TunnelExitedUnexpectedly -= OnTunnelExitedUnexpectedly;

            if (GetState().Status is not (TunnelStatus.Starting or TunnelStatus.Running))
            {
                return;
            }

            using var cts = new CancellationTokenSource(DisposeStopTimeout);
            try
            {
                await StopAsync(cts.Token);
                _logger.LogInformation("Cloudflare tunnel stopped at shutdown");
            }
            catch (Exception ex)
            {
                _logger.LogError("Cloudflare tunnel did not stop cleanly at shutdown ({ExceptionType}): {Message}", ex.GetType().Name, ex.Message);
            }
        }

        public TunnelState GetState()
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }
}
