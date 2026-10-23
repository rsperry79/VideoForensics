using Fido2NetLib;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.WebApp.Auth;

internal static class WebAuthnOriginPolicy
{
    public static void Apply(HttpContext context, Fido2Configuration configuration, IForensicsConfiguration appConfiguration)
    {
        if (!Uri.TryCreate($"{context.Request.Scheme}://{context.Request.Host}", UriKind.Absolute, out Uri? origin))
        {
            return;
        }

        string host = origin.Host;
        bool isLocal = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
        bool isQuickTunnel = host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase);
        bool isConfiguredPublicHost = Uri.TryCreate(appConfiguration.InternetServerUrl, UriKind.Absolute, out Uri? configured)
            && configured.Host.Equals(host, StringComparison.OrdinalIgnoreCase);

        if (isLocal || isQuickTunnel || isConfiguredPublicHost)
        {
            configuration.Origins = configuration.Origins
                .Append(origin.GetLeftPart(UriPartial.Authority))
                .ToHashSet(StringComparer.Ordinal);
        }

        // The RP ID is read once, when the Fido2 options are first used. If the Internet address was saved after
        // that, the RP ID would stay stale until restart, so it follows the saved address on every request.
        if (Uri.TryCreate(appConfiguration.InternetServerUrl, UriKind.Absolute, out Uri? internetUrl)
            && internetUrl.Scheme == Uri.UriSchemeHttps
            && internetUrl.Host.Length > 0)
        {
            configuration.RPID = internetUrl.Host;
        }
    }
}
