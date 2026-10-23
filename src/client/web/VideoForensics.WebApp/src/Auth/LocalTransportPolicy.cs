namespace VideoForensics.Client.WebApp.Auth;

/// <summary>What a request to the local server should get, based on how it arrived.</summary>
public enum LocalTransportAction
{
    /// <summary>Serve the page normally. Passkeys work: the origin is https or a loopback host, which browsers treat as secure.</summary>
    Serve,

    /// <summary>Redirect to the Cloudflare tunnel's public address, which is https and supports passkeys.</summary>
    RedirectToTunnel,

    /// <summary>Redirect to this server's https address on the LAN.</summary>
    RedirectToHttps,

    /// <summary>No secure address is available. Show that passkeys cannot be used here, and do not offer them.</summary>
    PasskeyUnavailable,
}

/// <summary>
/// Chooses the response for a local request. Plain-http LAN addresses are not a secure context, so browsers will not run
/// passkeys there. The order is: serve secure or loopback requests; otherwise send the user to the tunnel if it is running,
/// then to https if it is available, and only then say passkeys are unavailable.
/// </summary>
public static class LocalTransportPolicy
{
    public static LocalTransportAction Decide(bool isSecureOrigin, bool isLoopback, bool tunnelRunning, bool httpsAvailable)
    {
        if (isSecureOrigin || isLoopback)
        {
            return LocalTransportAction.Serve;
        }

        if (tunnelRunning)
        {
            return LocalTransportAction.RedirectToTunnel;
        }

        return httpsAvailable ? LocalTransportAction.RedirectToHttps : LocalTransportAction.PasskeyUnavailable;
    }
}
