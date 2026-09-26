#if !__IOS__
using VideoForensics.Hosting.ServerDiscovery;
#endif
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.MauiApp.Services
{
    /// <summary>
    /// MAUI-specific implementation that provides server location information by querying the
    /// IServerLocationResolver and IServerLocationSettingsStore resolved at app startup.
    /// Displays whether the server was discovered locally via mDNS or is using a cached Internet URL.
    /// </summary>
    public class MauiServerLocationInformationService : IServerLocationInformationService
    {
#if !__IOS__
        private readonly IServerLocationSettingsStore _settingsStore;

        public MauiServerLocationInformationService(IServerLocationSettingsStore settingsStore)
        {
            _settingsStore = settingsStore;
        }
#else
        public MauiServerLocationInformationService(object? unused = null)
        {
        }
#endif

        /// <inheritdoc/>
        public Uri? CurrentServerAddress { get; set; }

        /// <inheritdoc/>
        public bool? IsLocalAddress { get; set; }

        /// <inheritdoc/>
        public string LocationDescription => CurrentServerAddress is null
                    ? "No server address is currently active"
                    : IsLocalAddress == true
                    ? "Connected via local network (mDNS discovery)"
                    : IsLocalAddress == false ? "Connected via cached Internet address" : "Server address is active";
    }
}
