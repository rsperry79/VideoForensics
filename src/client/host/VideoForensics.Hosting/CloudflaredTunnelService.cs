using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;

namespace VideoForensics.Hosting
{
    public enum TunnelKind
    {
        None,
        Quick,
        Named
    }

    public enum TunnelStatus
    {
        Stopped,
        Starting,
        Running,
        Failed
    }

    /// <summary>Snapshot of the managed cloudflared process's current state, for the Remote Access screen (plan §5.3).</summary>
    public record TunnelState(TunnelKind Kind, TunnelStatus Status, string? PublicUrl, string? ErrorMessage, IReadOnlyList<string> RecentLogLines);

    /// <summary>
    /// Manages a single `cloudflared` child process exposing this server to the internet (plan
    /// §5.3), via CliWrap rather than hand-rolled <see cref="System.Diagnostics.Process"/>
    /// event-wiring - CliWrap's cancellation model force-kills the whole process tree
    /// cross-platform on cancel, which is what a hand-rolled <c>Kill(entireProcessTree: true)</c>
    /// was doing manually before.
    ///
    /// Two modes: a "quick tunnel" (`cloudflared tunnel --url ...`) needs no Cloudflare account and
    /// gets an ephemeral *.trycloudflare.com URL, parsed out of the process's own stderr; a "named
    /// tunnel" (`cloudflared tunnel run &lt;name&gt;`) runs a tunnel the operator already configured
    /// via `cloudflared tunnel login`/`create`/DNS route at the CLI - that one-time OAuth + DNS
    /// setup is deliberately NOT automated here (plan §5.3 says this GUI "walks through" it, not
    /// that it replaces the CLI entirely), so named-tunnel support here is limited to listing what's
    /// already configured (`cloudflared tunnel list`) and starting/stopping it.
    ///
    /// Only one tunnel (of either kind) runs at a time, matching the plan's single "Internet tier"
    /// concept - there is no scenario where a server needs two simultaneous public tunnels.
    /// </summary>
    public interface ICloudflaredTunnelService
    {
        /// <summary>True if the `cloudflared` executable is reachable on PATH.</summary>
        Task<bool> IsInstalledAsync(CancellationToken ct);

        /// <summary>Lists tunnel names already configured for this machine's authenticated Cloudflare account (empty if never logged in, or if not installed).</summary>
        Task<IReadOnlyList<string>> ListNamedTunnelsAsync(CancellationToken ct);

        /// <summary>Starts an ephemeral quick tunnel pointing at the given local port. No-op if a tunnel is already starting/running.</summary>
        Task StartQuickTunnelAsync(int localPort, CancellationToken ct);

        /// <summary>Starts a previously-configured named tunnel by name. No-op if a tunnel is already starting/running.</summary>
        Task StartNamedTunnelAsync(string tunnelName, CancellationToken ct);

        /// <summary>Stops whichever tunnel is currently running, if any.</summary>
        Task StopAsync(CancellationToken ct);

        TunnelState GetState();
    }

    public class CloudflaredTunnelService : ICloudflaredTunnelService
    {
        private static readonly Regex QuickTunnelUrlPattern = new(@"https://[a-z0-9-]+\.trycloudflare\.com", RegexOptions.Compiled);
        private const int MaxLogLines = 100;

        private readonly ILogger<CloudflaredTunnelService> _logger;
        private readonly object _lock = new();

        private CancellationTokenSource? _cts;
        private TunnelKind _kind = TunnelKind.None;
        private TunnelStatus _status = TunnelStatus.Stopped;
        private string? _publicUrl;
        private string? _errorMessage;
        private readonly LinkedList<string> _logLines = new();

        public CloudflaredTunnelService(ILogger<CloudflaredTunnelService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> IsInstalledAsync(CancellationToken ct)
        {
            try
            {
                var result = await Cli.Wrap("cloudflared")
                    .WithArguments("--version")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IReadOnlyList<string>> ListNamedTunnelsAsync(CancellationToken ct)
        {
            try
            {
                var result = await Cli.Wrap("cloudflared")
                    .WithArguments("tunnel list")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);
                if (result.ExitCode != 0)
                {
                    return Array.Empty<string>();
                }

                // Tabular output: "ID   NAME   CREATED   CONNECTIONS" - the header line's first
                // column starts with "ID", skip it; take the 2nd whitespace-separated column
                // (name) from every subsequent non-blank line. Best-effort parse of a CLI table
                // format cloudflared doesn't guarantee never changes; a name we fail to parse is
                // just missing from the picker, not a functional break.
                return result.StandardOutput
                    .Split('\n')
                    .Select(l => l.TrimEnd('\r'))
                    .Where(l => l.Length > 0 && !l.StartsWith("ID", StringComparison.OrdinalIgnoreCase))
                    .Select(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    .Where(parts => parts.Length >= 2)
                    .Select(parts => parts[1])
                    .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public Task StartQuickTunnelAsync(int localPort, CancellationToken ct)
            => StartAsync(TunnelKind.Quick, $"tunnel --url http://localhost:{localPort}");

        public Task StartNamedTunnelAsync(string tunnelName, CancellationToken ct)
            => StartAsync(TunnelKind.Named, $"tunnel run {tunnelName}");

        private Task StartAsync(TunnelKind kind, string arguments)
        {
            CancellationTokenSource cts;
            lock (_lock)
            {
                if (_status is TunnelStatus.Starting or TunnelStatus.Running)
                {
                    return Task.CompletedTask;
                }

                _kind = kind;
                _status = TunnelStatus.Starting;
                _publicUrl = null;
                _errorMessage = null;
                _logLines.Clear();
                _cts = cts = new CancellationTokenSource();
            }

            // Fire-and-forget: the event loop below runs for the tunnel's whole lifetime and only
            // ends when StopAsync cancels `cts` or cloudflared exits on its own - GetState() is how
            // callers observe progress, not awaiting this task.
            _ = RunAsync(kind, arguments, cts);

            _logger.LogInformation("cloudflared tunnel starting ({Kind}): {Arguments}", kind, arguments);
            return Task.CompletedTask;
        }

        private async Task RunAsync(TunnelKind kind, string arguments, CancellationTokenSource cts)
        {
            var command = Cli.Wrap("cloudflared")
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None);

            try
            {
                await foreach (var cmdEvent in command.ListenAsync(cts.Token))
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent:
                            lock (_lock)
                            {
                                if (ReferenceEquals(_cts, cts))
                                {
                                    _status = TunnelStatus.Running;
                                }
                            }
                            break;

                        case StandardOutputCommandEvent stdOut:
                            OnLine(cts, stdOut.Text);
                            break;

                        case StandardErrorCommandEvent stdErr:
                            OnLine(cts, stdErr.Text);
                            break;

                        case ExitedCommandEvent exited:
                            lock (_lock)
                            {
                                if (ReferenceEquals(_cts, cts))
                                {
                                    _status = exited.ExitCode == 0 ? TunnelStatus.Stopped : TunnelStatus.Failed;
                                    _errorMessage ??= exited.ExitCode == 0 ? null : $"cloudflared exited with code {exited.ExitCode}";
                                    _cts = null;
                                }
                            }
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when StopAsync() cancels `cts` - state was already set to Stopped there.
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_cts, cts))
                    {
                        _status = TunnelStatus.Failed;
                        _errorMessage = $"cloudflared failed: {ex.Message}";
                        _cts = null;
                    }
                }

                _logger.LogError(ex, "cloudflared tunnel ({Kind}) failed", kind);
            }
        }

        private void OnLine(CancellationTokenSource cts, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            lock (_lock)
            {
                if (!ReferenceEquals(_cts, cts))
                {
                    return;
                }

                _logLines.AddLast(line);
                while (_logLines.Count > MaxLogLines)
                {
                    _logLines.RemoveFirst();
                }

                if (_publicUrl is null)
                {
                    var match = QuickTunnelUrlPattern.Match(line);
                    if (match.Success)
                    {
                        _publicUrl = match.Value;
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken ct)
        {
            CancellationTokenSource? cts;
            lock (_lock)
            {
                cts = _cts;
                _cts = null;
                _status = TunnelStatus.Stopped;
                _publicUrl = null;
                _kind = TunnelKind.None;
            }

            // Cancelling the token CliWrap is observing force-kills the whole cloudflared process
            // tree cross-platform - the library's replacement for a hand-rolled
            // Process.Kill(entireProcessTree: true).
            cts?.Cancel();
            cts?.Dispose();

            _logger.LogInformation("cloudflared tunnel stopped");
            return Task.CompletedTask;
        }

        public TunnelState GetState()
        {
            lock (_lock)
            {
                return new TunnelState(_kind, _status, _publicUrl, _errorMessage, _logLines.ToList());
            }
        }
    }
}
