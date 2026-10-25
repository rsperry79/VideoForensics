using System.Net;

using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.WebApp.Auth;

/// <summary>What the transport middleware knows about this server's secure addresses.</summary>
/// <param name="HttpsPort">Port of the https listener, or null when none is configured.</param>
/// <param name="HttpsCertificateReady">True once a certificate is loaded for that listener.</param>
public sealed record LocalTransportOptions(int? HttpsPort, Func<bool> HttpsCertificateReady);

/// <summary>
/// Applies <see cref="LocalTransportPolicy"/> to plain-http requests from the LAN. Requests from loopback (which includes
/// the Cloudflare tunnel's own connector) are always served, so the tunnel cannot redirect to itself in a loop.
/// </summary>
public sealed class LocalTransportMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LocalTransportMiddleware> _logger;

    public LocalTransportMiddleware(RequestDelegate next, ILogger<LocalTransportMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICloudflaredTunnelService tunnels, LocalTransportOptions options)
    {
        if (!IsPageRequest(context.Request))
        {
            await _next(context);
            return;
        }

        await ApplyPolicyAsync(context, tunnels, options);
    }

    /// <summary>
    /// Only GET and HEAD page loads are redirected. API calls (which carry bodies, such as starting the tunnel), SignalR and
    /// Blazor connections, and other non-GET requests always pass through, since a redirect would drop them.
    /// </summary>
    internal static bool IsPageRequest(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        string path = request.Path.Value ?? string.Empty;
        return !(path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_", StringComparison.Ordinal)
            || path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase));
    }

    private async Task ApplyPolicyAsync(HttpContext context, ICloudflaredTunnelService tunnels, LocalTransportOptions options)
    {
        TunnelState tunnel = tunnels.GetState();
        bool tunnelRunning = tunnel.Status == TunnelStatus.Running
            && Uri.TryCreate(tunnel.PublicUrl, UriKind.Absolute, out _);
        bool httpsAvailable = options.HttpsPort is not null && options.HttpsCertificateReady();

        LocalTransportAction action = LocalTransportPolicy.Decide(
            isSecureOrigin: context.Request.IsHttps,
            isLoopback: IsLoopbackRequest(context),
            tunnelRunning,
            httpsAvailable);

        switch (action)
        {
            case LocalTransportAction.RedirectToTunnel:
                string tunnelTarget = tunnel.PublicUrl!.TrimEnd('/') + context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                _logger.LogInformation("Redirecting plain-http LAN request to the Cloudflare tunnel so passkeys can be used");
                context.Response.Redirect(tunnelTarget, permanent: false);
                return;

            case LocalTransportAction.RedirectToHttps:
                string host = context.Request.Host.Host;
                string httpsTarget = $"https://{host}:{options.HttpsPort}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
                _logger.LogInformation("Redirecting plain-http LAN request to https so passkeys can be used");
                context.Response.Redirect(httpsTarget, permanent: false);
                return;

            case LocalTransportAction.PasskeyUnavailable:
                // Not blocked: password sign-in still works. The message is surfaced to the page, which reads this flag.
                context.Items[PasskeyUnavailableItemKey] = true;
                _logger.LogWarning("Passkeys are unavailable on this address: it is plain http and no https address or tunnel is available");
                break;
        }

        await _next(context);
    }

    /// <summary>
    /// A loopback request is served here, so a browser on this machine can sign in with its password. Passkeys cannot work
    /// on localhost (the RP ID is the tunnel host), so a passkey attempt that fails sends the browser to the tunnel instead.
    /// </summary>
    private static bool IsLoopbackRequest(HttpContext context)
    {
        return context.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote);
    }

    /// <summary>HttpContext.Items key the sign-in page reads to show the "passkeys unavailable here" message.</summary>
    public const string PasskeyUnavailableItemKey = "VideoForensics.PasskeyUnavailable";
}
