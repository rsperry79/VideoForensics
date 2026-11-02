using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using VideoForensics.Utils.DbRepair.Contracts;

namespace VideoForensics.Utils.DbRepair;

/// <summary>
/// Puts the running server into maintenance before the repair tool writes the database file, so the server does not keep
/// the file open or cache what it read. The server only holds maintenance; this tool does the repair itself.
/// Never prints an API key, a device code or a hold token.
/// </summary>
public sealed class ServerMaintenanceClient
{
    /// <summary>The address the server listens on locally when no <c>--server</c> is given.</summary>
    public static readonly Uri DefaultServer = new("http://localhost:5162/");

    /// <summary>How often a held maintenance run is renewed. Well inside the server's 30-minute safety timeout.</summary>
    public static readonly TimeSpan RenewInterval = TimeSpan.FromMinutes(10);

    /// <summary>How long without a successful renew before the hold is treated as lost. Stays inside the server's 30-minute safety timeout.</summary>
    public static readonly TimeSpan RenewStaleAfter = TimeSpan.FromMinutes(25);

    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MinPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PairingTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EndTimeout = TimeSpan.FromSeconds(30);

    private const string NoHoldQuestion = "Continue without putting the server into maintenance? [y/N]";

    private readonly HttpClient _http;
    private readonly IMaintenanceConsole _console;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeProvider _time;

    /// <param name="http">Client for the server. Its timeout should be infinite: the begin call waits for in-flight work to drain.</param>
    /// <param name="console">Operator input and output.</param>
    /// <param name="delay">Wait used between poll attempts and renewals. Tests replace it so they do not wait in real time.</param>
    /// <param name="time">Clock for the renew-staleness rule. Tests replace it; null uses the system clock.</param>
    public ServerMaintenanceClient(HttpClient http, IMaintenanceConsole console, Func<TimeSpan, CancellationToken, Task>? delay = null, TimeProvider? time = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _delay = delay ?? ((wait, ct) => Task.Delay(wait, ct));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Checks a <c>--server</c> value. Plain http is accepted only for the loopback hosts, so the operator's pairing key never
    /// crosses the network in clear text. Any other host must use https.
    /// </summary>
    /// <param name="value">The URL the operator typed.</param>
    /// <param name="server">The parsed address when valid; otherwise null.</param>
    /// <param name="error">A message for the operator when invalid; otherwise null.</param>
    public static bool TryValidateServer(string? value, out Uri? server, out string? error)
    {
        server = null;
        error = null;

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            error = "--server must be an absolute URL, for example http://localhost:5162/.";
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            server = uri;
            return true;
        }

        if (uri.Scheme == Uri.UriSchemeHttp && IsLoopbackHost(uri.Host))
        {
            server = uri;
            return true;
        }

        error = uri.Scheme == Uri.UriSchemeHttp
            ? "--server uses plain http, which is allowed only for localhost, 127.0.0.1 or [::1]. Use an https URL for any other host."
            : "--server must be an http URL on the local machine or an https URL.";
        return false;
    }

    /// <summary>
    /// Decides whether the repair may run, and holds server maintenance when it can. Returns a session when the repair
    /// may proceed (check <see cref="ServerMaintenanceSession.Held"/>), or null when the operator declined. A null result
    /// means the database must not be touched.
    /// </summary>
    /// <param name="server">Base address of the local server.</param>
    /// <param name="ct">Cancellation token for the sign-in and the begin call.</param>
    /// <param name="repairStop">Source the repair listens to. Cancelled when the server ends the hold early, so the repair writes nothing further. Null when nothing needs to be stopped.</param>
    public async Task<ServerMaintenanceSession?> StartAsync(Uri server, CancellationToken ct, CancellationTokenSource? repairStop = null)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (!await IsReachableAsync(server, ct))
        {
            _console.Write($"The local server could not be reached at {server}.");
            return await ConfirmNoHoldAsync(ct) ? ServerMaintenanceSession.WithoutHold() : null;
        }

        var pairing = await SignInAsync(server, ct);
        if (pairing is null)
        {
            return await ConfirmNoHoldAsync(ct) ? ServerMaintenanceSession.WithoutHold() : null;
        }

        (BeginOutcome outcome, Guid? holdToken) = await BeginHoldAsync(server, pairing.Value.ApiKey, ct);
        switch (outcome)
        {
            case BeginOutcome.Held:
                _console.Write("Server is in maintenance: new work is held and database connections are released.");
                return new ServerMaintenanceSession(_http, server, pairing.Value.ApiKey, holdToken!.Value, _console, _delay, _time, repairStop);
            case BeginOutcome.InProgress:
                _console.Write("Server maintenance is already in progress.");
                return await ConfirmNoHoldAsync(ct) ? ServerMaintenanceSession.WithoutHold() : null;
            case BeginOutcome.Drained:
                // Why: the drain message was already printed. No prompt: the operator retries once the server is idle.
                return null;
            default:
                return await ConfirmNoHoldAsync(ct) ? ServerMaintenanceSession.WithoutHold() : null;
        }
    }

    private static bool IsLoopbackHost(string host) => host is "localhost" or "127.0.0.1" or "[::1]" or "::1";

    // Why: asked after every failure to hold. Non-interactive runs get no, so a scripted run never touches the database unheld.
    private async Task<bool> ConfirmNoHoldAsync(CancellationToken ct)
    {
        if (!_console.IsInteractive)
        {
            _console.Write("Input is redirected, so the repair will not continue without maintenance.");
            return false;
        }

        return await _console.ConfirmAsync(NoHoldQuestion, ct);
    }

    private async Task<bool> IsReachableAsync(Uri server, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(HealthTimeout);
        try
        {
            // Why: any HTTP response means a server is listening, even an unhealthy one, so it can still hold maintenance.
            using HttpResponseMessage response = await _http.GetAsync(new Uri(server, "healthz"), cts.Token);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The health timeout fired.
            return false;
        }
    }

    // Returns the API key once the operator approves the code in the browser, or null when pairing failed or timed out.
    private async Task<(string ApiKey, string DeviceCode)?> SignInAsync(Uri server, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage start = await _http.PostAsync(new Uri(server, "api/v1/pairing/device-code"), null, ct);
            if (!start.IsSuccessStatusCode)
            {
                _console.Write($"Pairing could not start (HTTP {(int)start.StatusCode}).");
                return null;
            }

            using JsonDocument startBody = JsonDocument.Parse(await start.Content.ReadAsStringAsync(ct));
            string deviceCode = startBody.RootElement.GetProperty("deviceCode").GetString() ?? throw new InvalidDataException("Missing device code.");
            string userCode = startBody.RootElement.GetProperty("userCode").GetString() ?? throw new InvalidDataException("Missing user code.");
            TimeSpan pollInterval = PollIntervalFrom(startBody.RootElement);

            Uri pairingPage = new(server, $"pair/device?code={Uri.EscapeDataString(userCode)}");
            _console.Write($"Pairing code: {userCode}");
            _console.Write("Approve this console in the browser (a local Super Admin signs in there).");
            if (!_console.OpenBrowser(pairingPage))
            {
                _console.Write($"Open this page to approve: {pairingPage}");
            }

            string? apiKey = await PollForApiKeyAsync(server, deviceCode, pollInterval, ct);
            return apiKey is null ? null : (apiKey, deviceCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _console.Write($"Pairing failed: {ex.Message} ({ex.GetType().Name})");
            return null;
        }
    }

    // Why: the server states how often it accepts polls. Polling faster only earns rate limits, so the console never goes below 2 seconds
    // and falls back to the server's usual 5 seconds when the field is missing.
    private static TimeSpan PollIntervalFrom(JsonElement root)
    {
        if (!root.TryGetProperty("intervalSeconds", out JsonElement interval) || !interval.TryGetInt32(out int seconds))
        {
            return DefaultPollInterval;
        }

        return TimeSpan.FromSeconds(Math.Max(seconds, MinPollInterval.TotalSeconds));
    }

    private async Task<string?> PollForApiKeyAsync(Uri server, string deviceCode, TimeSpan pollInterval, CancellationToken ct)
    {
        Uri pollUri = new(server, $"api/v1/pairing/device-code/{Uri.EscapeDataString(deviceCode)}/poll");
        DateTimeOffset deadline = DateTimeOffset.UtcNow + PairingTimeout;
        TimeSpan? retryAfter = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            // Why: a rate limit names how long to wait, so honour it; the wait never runs past the pairing deadline.
            // Why: a Retry-After of 0 or a value below the server interval would make the console re-poll back to back, so the wait is floored at both.
            TimeSpan wait = retryAfter is { } limitedWait ? TimeSpan.FromTicks(Math.Max(limitedWait.Ticks, Math.Max(pollInterval.Ticks, MinPollInterval.Ticks))) : pollInterval;
            TimeSpan untilDeadline = deadline - DateTimeOffset.UtcNow;
            if (wait > untilDeadline)
            {
                wait = untilDeadline;
            }

            retryAfter = null;
            await _delay(wait < TimeSpan.Zero ? TimeSpan.Zero : wait, ct);

            using HttpResponseMessage response = await _http.PostAsync(pollUri, null, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _console.Write("The pairing code expired.");
                return null;
            }

            // Why: rate limits and server errors are retryable. The next attempt waits the same interval, and the 10-minute deadline still bounds the wait.
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    retryAfter = RetryAfterFrom(response);
                }

                _console.Write($"Pairing is busy (HTTP {(int)response.StatusCode}); retrying.");
                continue;
            }

            if (IsRedirect(response.StatusCode))
            {
                _console.Write(RedirectMessage(response.StatusCode));
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _console.Write($"Pairing poll failed (HTTP {(int)response.StatusCode}).");
                return null;
            }

            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            string status = body.RootElement.GetProperty("status").GetString() ?? string.Empty;
            switch (status)
            {
                case "approved":
                    return body.RootElement.GetProperty("apiKey").GetString();
                case "expired":
                    _console.Write("The pairing code expired.");
                    return null;
            }
        }

        _console.Write("Pairing timed out after 10 minutes.");
        return null;
    }

    // Why: only the seconds form is read. A missing or date-form header falls back to the normal poll interval.
    private static TimeSpan? RetryAfterFrom(HttpResponseMessage response)
    {
        TimeSpan? delta = response.Headers.RetryAfter?.Delta;
        return delta is { } seconds && seconds >= TimeSpan.Zero ? seconds : null;
    }

    // Why: every request that carries the key uses a client that does not follow redirects, so a 3xx is reported and never followed.
    private static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and < 400;

    private static string RedirectMessage(HttpStatusCode status) =>
        $"The server answered with a redirect (HTTP {(int)status}). Redirects are not followed, so the API key is not sent to another address.";

    private async Task<(BeginOutcome Outcome, Guid? HoldToken)> BeginHoldAsync(Uri server, string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(server, "api/v1/database/maintenance/begin"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        try
        {
            using HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return (BeginOutcome.InProgress, null);
            }

            if (IsRedirect(response.StatusCode))
            {
                _console.Write(RedirectMessage(response.StatusCode));
                return (BeginOutcome.Failed, null);
            }

            // Why: 503 means the server's drain timed out. Asking whether to continue without maintenance would be wrong here, because the server is still busy.
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                _console.Write("the server did not finish its work in time; try again later");
                return (BeginOutcome.Drained, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                _console.Write($"The server did not start maintenance (HTTP {(int)response.StatusCode}).");
                return (BeginOutcome.Failed, null);
            }

            // Why: a begin that succeeded without a token cannot be ended or renewed by this run. Treat it as a failure, and let the server timer recover it.
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!body.RootElement.TryGetProperty("holdToken", out JsonElement tokenElement) || !tokenElement.TryGetGuid(out Guid token))
            {
                _console.Write("The server started maintenance but returned no hold token. It ends on its own safety timeout.");
                return (BeginOutcome.Failed, null);
            }

            return (BeginOutcome.Held, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Why: when the request was sent but its response was lost, the token is unknown, so no end call is made. The server's safety timeout ends the hold. Do not invent a token.
            _console.Write($"The server did not start maintenance: {ex.Message} ({ex.GetType().Name}). If it did start, the server ends maintenance on its own safety timeout.");
            return (BeginOutcome.Failed, null);
        }
    }

    private enum BeginOutcome
    {
        Held,
        InProgress,
        Drained,
        Failed,
    }

    /// <summary>
    /// A maintenance hold taken by <see cref="ServerMaintenanceClient"/>. Renews the hold in the background while it is open,
    /// and disposing it ends the hold with its token. Holds nothing when <see cref="Held"/> is false, so the repair can run
    /// after the operator accepts running without maintenance.
    /// </summary>
    public sealed class ServerMaintenanceSession : IAsyncDisposable
    {
        private readonly HttpClient? _http;
        private readonly Uri? _server;
        private readonly string? _apiKey;
        private readonly Guid? _holdToken;
        private readonly IMaintenanceConsole? _console;
        private readonly Func<TimeSpan, CancellationToken, Task>? _delay;
        private readonly CancellationTokenSource? _renewCancel;
        private readonly Task? _renewLoop;
        private readonly CancellationTokenSource? _repairStop;
        private readonly TimeProvider _time = TimeProvider.System;
        private volatile bool _holdLost;
        // Why: set when the hold starts and on each successful renew, so the 25-minute rule counts from the last time the server accepted a renew.
        private DateTimeOffset _lastRenewUtc;

        internal ServerMaintenanceSession(HttpClient http, Uri server, string apiKey, Guid holdToken, IMaintenanceConsole console, Func<TimeSpan, CancellationToken, Task> delay, TimeProvider time, CancellationTokenSource? repairStop = null)
        {
            _http = http;
            _server = server;
            _apiKey = apiKey;
            _holdToken = holdToken;
            _console = console;
            _delay = delay;
            _time = time;
            _lastRenewUtc = time.GetUtcNow();
            _repairStop = repairStop;
            _renewCancel = new CancellationTokenSource();
            _renewLoop = RenewUntilCancelledAsync(_renewCancel.Token);
        }

        private ServerMaintenanceSession()
        {
        }

        /// <summary>True when the server is holding maintenance for this run.</summary>
        public bool Held => _holdToken is not null;

        /// <summary>True when the hold was lost before the repair finished: the server answered 401, 403, 404, 409 or a redirect to a renew, or no renew succeeded for 25 minutes. The repair must then be treated as failed.</summary>
        public bool HoldLost => _holdLost;

        /// <summary>A session that holds nothing. Used when the operator accepted running without maintenance.</summary>
        public static ServerMaintenanceSession WithoutHold() => new();

        /// <summary>
        /// Renews the hold once, right before the repair commits. Returns true only when the server accepted that renew.
        /// A refusal that ends the hold (401, 403, 404, 409, redirect) marks it lost and stops the repair. Any other failure returns false
        /// without marking the hold lost, so the commit is still skipped. Returns true when nothing is held.
        /// </summary>
        /// <param name="ct">Cancellation token for the renew call.</param>
        public async Task<bool> ConfirmHoldAsync(CancellationToken ct)
        {
            if (!Held)
            {
                return true;
            }

            try
            {
                using HttpResponseMessage response = await PostWithTokenAsync("api/v1/database/maintenance/renew", ct);
                if (response.IsSuccessStatusCode)
                {
                    _lastRenewUtc = _time.GetUtcNow();
                    return true;
                }

                if (EndsHold(response.StatusCode))
                {
                    _console!.Write("The server ended maintenance before the commit; the repair will not commit.");
                    LoseHold();
                    return false;
                }

                _console!.Write($"Could not confirm server maintenance before the commit (HTTP {(int)response.StatusCode}); the repair will not commit.");
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _console!.Write($"Could not confirm server maintenance before the commit: {ex.Message} ({ex.GetType().Name}); the repair will not commit.");
                return false;
            }
        }

        /// <summary>Stops renewing, then ends the hold. Never throws: a failure prints the way to end maintenance by hand.</summary>
        public async ValueTask DisposeAsync()
        {
            if (!Held)
            {
                return;
            }

            _renewCancel!.Cancel();
            await _renewLoop!;
            _renewCancel.Dispose();

            // The key stays valid after the hold ends, whether or not the hold was lost; the operator has to revoke it, because revoke needs step-up.
            _console!.Write("The pairing key is still valid: revoke this device on the admin page when you are done.");

            // Why: the server already ended the hold, so an end call would only fail with 404 and print a misleading error.
            if (_holdLost)
            {
                return;
            }

            using var cts = new CancellationTokenSource(EndTimeout);
            try
            {
                using HttpResponseMessage response = await PostWithTokenAsync("api/v1/database/maintenance/end", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    _console.Write("Server maintenance ended.");
                    return;
                }

                if (IsRedirect(response.StatusCode))
                {
                    _console.Write(RedirectMessage(response.StatusCode) + " End it from the admin page or restart the server.");
                    return;
                }

                _console.Write($"Could not end server maintenance (HTTP {(int)response.StatusCode}). End it from the admin page or restart the server.");
            }
            catch (Exception ex)
            {
                _console.Write($"Could not end server maintenance: {ex.Message} ({ex.GetType().Name}). End it from the admin page or restart the server.");
            }
        }

        // Renews the hold every RenewInterval until cancelled. Stops early when the server no longer knows the token, since the hold has ended.
        private async Task RenewUntilCancelledAsync(CancellationToken ct)
        {
            while (true)
            {
                try
                {
                    await _delay!(RenewInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                bool renewed = false;
                try
                {
                    using HttpResponseMessage response = await PostWithTokenAsync("api/v1/database/maintenance/renew", ct);
                    // Why: 401 and 403 (pairing revoked), 404 (token not active), 409 (max age reached) and a redirect all mean the hold will not be renewed. Stop the repair
                    // before it writes more, and do not retry. The caller prints the single message that explains the state.
                    if (EndsHold(response.StatusCode))
                    {
                        LoseHold();
                        return;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        _lastRenewUtc = _time.GetUtcNow();
                        renewed = true;
                    }
                    else
                    {
                        _console!.Write($"Could not renew server maintenance (HTTP {(int)response.StatusCode}); will retry.");
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _console!.Write($"Could not renew server maintenance: {ex.Message} ({ex.GetType().Name}); will retry.");
                }

                // Why: a server that keeps failing renews may still hold the run, but the last accepted renew is too old to trust. Treat the hold as lost, the same as an explicit refusal.
                if (!renewed && IsRenewStale(_time.GetUtcNow() - _lastRenewUtc))
                {
                    _console!.Write("No server maintenance renew has succeeded for 25 minutes; treating the hold as lost.");
                    LoseHold();
                    return;
                }
            }
        }

        // Why: decided in one place so the staleness rule and the explicit refusals end the repair the same way.
        private void LoseHold()
        {
            _holdLost = true;
            _repairStop?.Cancel();
        }

        // Why: with renews every 10 minutes, 25 minutes tolerates two failed renews and fails on the third, before the server's 30-minute safety timeout ends the hold on its own.
        internal static bool IsRenewStale(TimeSpan sinceLastRenew) => sinceLastRenew >= RenewStaleAfter;

        // Why: 401 or 403 means the pairing was revoked mid-run, so the key no longer renews the hold. Treat it like the other lost-hold answers.
        private static bool EndsHold(HttpStatusCode status) =>
            status is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || IsRedirect(status);

        private async Task<HttpResponseMessage> PostWithTokenAsync(string path, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_server!, path))
            {
                Content = new StringContent(JsonSerializer.Serialize(new { holdToken = _holdToken }), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            // Why: awaited here so the request is not disposed while it is still being sent.
            return await _http!.SendAsync(request, ct);
        }
    }
}
