namespace VideoForensics.Client.Common
{
    /// <summary>
    /// The MAUI app-lock idle timeout (plan §5.9's <c>/settings/app-lock</c> screen: "lock after
    /// immediately / 1 min / 5 min / 15 min in background"). Deliberately a device-LOCAL setting,
    /// not a server AppSetting - the route table marks this screen "n/a (device-local)", not
    /// gated by any server RBAC role, since it only affects the one physical device it's set on.
    /// The MAUI implementation backs this with <c>Microsoft.Maui.Storage.Preferences</c>; other
    /// hosts (WebApp, console, MCP) register the no-op default below, since app-lock has no
    /// meaning for a server process.
    /// </summary>
    public interface IAppLockPreferencesStore
    {
        /// <summary>False on any host where app-lock doesn't apply (everything except MAUI) - the settings page uses this to show "not applicable on this host" instead of a broken control.</summary>
        bool IsSupported { get; }

        TimeSpan GetIdleLockTimeout();
        void SetIdleLockTimeout(TimeSpan timeout);
    }

    public class NullAppLockPreferencesStore : IAppLockPreferencesStore
    {
        public bool IsSupported => false;
        public TimeSpan GetIdleLockTimeout() => TimeSpan.Zero;
        public void SetIdleLockTimeout(TimeSpan timeout) { }
    }
}
