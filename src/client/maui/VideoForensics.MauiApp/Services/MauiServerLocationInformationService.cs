using VideoForensics.Hosting.ServerDiscovery;
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
        private readonly IServerLocationSettingsStore _settingsStore;

        public MauiServerLocationInformationService(IServerLocationSettingsStore settingsStore)
        {
            _settingsStore = settingsStore;
        }

        /// <inheritdoc/>
        public Uri? CurrentServerAddress { get; set; }

        /// <inheritdoc/>
        public bool? IsLocalAddress { get; set; }

        /// <inheritdoc/>
        public string LocationDescription
        {
            get
            {
                if (CurrentServerAddress is null)
                {
                    return "No server address is currently active";
                }

                if (IsLocalAddress == true)
                {
                    return "Connected via local network (mDNS discovery)";
                }
                else if (IsLocalAddress == false)
                {
                    return "Connected via cached Internet address";
                }
                else
                {
                    return "Server address is active";
                }
            }
        }
    }
}
