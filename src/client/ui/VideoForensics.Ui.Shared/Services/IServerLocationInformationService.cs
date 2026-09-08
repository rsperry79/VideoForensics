namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Provides read-only information about the currently active server connection address.
    /// Displays whether the server was discovered locally via mDNS or is using a cached Internet URL,
    /// along with the actual address being used.
    /// </summary>
    public interface IServerLocationInformationService
    {
        /// <summary>
        /// Gets the currently active server address, if available.
        /// </summary>
        Uri? CurrentServerAddress { get; }

        /// <summary>
        /// Gets whether the current address is a local mDNS-discovered address (true) or a cached Internet URL (false).
        /// Null if no address is currently active or known.
        /// </summary>
        bool? IsLocalAddress { get; }

        /// <summary>
        /// Gets a user-friendly description of the current server location (e.g., "Local (mDNS)" or "Cached Internet URL").
        /// </summary>
        string LocationDescription { get; }
    }
}
