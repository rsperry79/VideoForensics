using CloudflaredKit;

using Microsoft.Extensions.Options;

namespace VideoForensics.Hosting
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
        Task StartPermanentTunnelAsync(string tunnelToken, CancellationToken ct);
        Task StopAsync(CancellationToken ct);
        TunnelState GetState();
    }

    public sealed class CloudflaredTunnelService : ICloudflaredTunnelService
    {
        private readonly ICloudflaredService _cloudflared;
        private readonly IOptionsMonitorCache<CloudflaredOptions> _optionsCache;
        private readonly object _gate = new();
        private TunnelState _state = new(
            TunnelKind.None,
            TunnelStatus.Stopped,
            null,
            null,
            Array.Empty<string>());

        public CloudflaredTunnelService(
            ICloudflaredService cloudflared,
            IOptionsMonitorCache<CloudflaredOptions> optionsCache)
        {
            _cloudflared = cloudflared;
            _optionsCache = optionsCache;
        }

        public async Task StartPermanentTunnelAsync(string tunnelToken, CancellationToken ct)
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
                _optionsCache.TryRemove(Options.DefaultName);
                _optionsCache.TryAdd(Options.DefaultName, new CloudflaredOptions
                {
                    TunnelToken = tunnelToken
                });

                await _cloudflared.StartAsync(ct);
                lock (_gate)
                {
                    _state = _state with { Status = TunnelStatus.Running };
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
                lock (_gate)
                {
                    _state = _state with
                    {
                        Status = TunnelStatus.Failed,
                        ErrorMessage = ex.Message
                    };
                }

                throw;
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

        public TunnelState GetState()
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }
}
