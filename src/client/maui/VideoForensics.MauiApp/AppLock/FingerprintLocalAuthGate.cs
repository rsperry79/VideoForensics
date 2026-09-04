using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;
using VideoForensics.Client.Common;

namespace VideoForensics.MauiApp.AppLock
{
    /// <summary>
    /// Windows implementation of the local app-lock gate (plan §5.9), via Plugin.Fingerprint.Maui
    /// rather than calling <c>Windows.Security.Credentials.UI.UserConsentVerifier</c> directly -
    /// the plugin wraps that same WinRT API (Windows Hello) behind a small cross-platform surface,
    /// so a future iOS/Android target reuses this class instead of a Windows-specific rewrite.
    /// Falls back to the OS's own PIN/password prompt when biometrics aren't enrolled - that is
    /// standard Windows Hello behavior the plugin inherits, not custom fallback logic here.
    /// </summary>
    public class FingerprintLocalAuthGate : ILocalAuthGate
    {
        public Task<bool> IsAvailableAsync() => CrossFingerprint.Current.IsAvailableAsync(allowAlternativeAuthentication: true);

        public async Task<bool> AuthenticateAsync(string reason, CancellationToken ct)
        {
            var config = new AuthenticationRequestConfiguration("VideoForensics", reason)
            {
                AllowAlternativeAuthentication = true
            };

            var result = await CrossFingerprint.Current.AuthenticateAsync(config, ct);
            return result.Authenticated;
        }
    }
}
