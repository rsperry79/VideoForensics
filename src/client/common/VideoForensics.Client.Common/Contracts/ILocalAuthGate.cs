namespace VideoForensics.Client.Common
{
    /// <summary>
    /// Local device biometric/PIN gate (plan §5.9) - distinct from and in addition to §5.1's
    /// server-pairing passkey auth: that governs whether a device can talk to the server's API at
    /// all, this governs whether whoever is physically holding an already-paired, already-signed-in
    /// device can see anything in the UI. Picking up an unlocked laptop shouldn't be enough on its
    /// own to browse evidence.
    ///
    /// Deliberately a small cross-platform-capable abstraction rather than calling a platform API
    /// (Windows Hello via <c>Windows.Security.Credentials.UI.UserConsentVerifier</c>) directly from
    /// UI code, per the plan - Windows is the only implementation today
    /// (<c>FingerprintLocalAuthGate</c> in VideoForensics.MauiApp, wrapping Plugin.Fingerprint.Maui,
    /// which itself wraps that same WinRT API), but adding iOS/Android later reuses this gate
    /// instead of a redesign. No implementation is registered on non-MAUI hosts (WebApp, console,
    /// MCP) - app-lock is a device-local concept with no meaning for a server process.
    /// </summary>
    public interface ILocalAuthGate
    {
        Task<bool> IsAvailableAsync();

        /// <summary>Prompts the platform's biometric/PIN UI. True only on an explicit successful verification - never assume unavailable/cancelled means "allow".</summary>
        Task<bool> AuthenticateAsync(string reason, CancellationToken ct);
    }
}
