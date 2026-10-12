namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Default implementation for hosts where server location information is not available or not applicable
    /// (e.g., WebApp where the current process IS the server, or other scenarios without local discovery).
    /// Returns null/empty values indicating server location info is unavailable.
    /// </summary>
    public class DefaultServerLocationInformationService : IServerLocationInformationService
    {
        /// <inheritdoc/>
        public Uri? CurrentServerAddress => null;

        /// <inheritdoc/>
        public bool? IsLocalAddress => null;

        /// <inheritdoc/>
        public string LocationDescription => "Server location information not available on this host";
    }
}
