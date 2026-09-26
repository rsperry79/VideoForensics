#if !__IOS__
using VideoForensics.Hosting.ServerDiscovery;
#endif

namespace VideoForensics.MauiApp.ServerDiscovery
{
    /// <summary>
    /// Backs server location settings (cached Internet URL) with MAUI's local Preferences store -
    /// per-device, never synced to the server, matching the route table's "n/a (device-local)" note.
    /// </summary>
    public class MauiServerLocationSettingsStore : 
#if !__IOS__
        IServerLocationSettingsStore
#else
        object // iOS fallback: not used in iOS code path
#endif
    {
        private const string CachedInternetServerUrlKey = "ServerLocation.CachedInternetServerUrl";

        public string? GetCachedInternetServerUrl()
        {
            return Preferences.Default.Get<string?>(CachedInternetServerUrlKey, null);
        }

        public void SetCachedInternetServerUrl(string url)
        {
            Preferences.Default.Set(CachedInternetServerUrlKey, url);
        }
    }
}
