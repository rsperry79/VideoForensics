using Microsoft.Maui.Storage;
using VideoForensics.Client.Common;

namespace VideoForensics.MauiApp.AppLock
{
    /// <summary>Backs the app-lock idle timeout (plan §5.9) with MAUI's local Preferences store - per-device, never synced to the server, matching the route table's "n/a (device-local)" note.</summary>
    public class MauiAppLockPreferencesStore : IAppLockPreferencesStore
    {
        private const string PreferenceKey = "AppLock.IdleTimeoutSeconds";
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

        public bool IsSupported => true;

        public TimeSpan GetIdleLockTimeout()
        {
            var seconds = Preferences.Default.Get(PreferenceKey, (int)DefaultTimeout.TotalSeconds);
            return TimeSpan.FromSeconds(seconds);
        }

        public void SetIdleLockTimeout(TimeSpan timeout)
        {
            Preferences.Default.Set(PreferenceKey, (int)timeout.TotalSeconds);
        }
    }
}
