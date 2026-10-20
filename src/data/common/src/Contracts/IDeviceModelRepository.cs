namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Writes provider device-model rows that a reference source does not list.</summary>
    public interface IDeviceModelRepository
    {
        /// <summary>
        /// Records a provider kind the catalog does not list, with no product name, so the database knows it exists.
        /// Does nothing when the kind already has a row, catalog or discovered.
        /// </summary>
        /// <param name="provider">Provider that reported the kind (for example "Ring").</param>
        /// <param name="kind">The raw kind string.</param>
        /// <param name="family">Product family taken from the device's type (for example "Doorbell").</param>
        /// <param name="ct">Cancellation token.</param>
        Task EnsureDiscoveredAsync(string provider, string kind, string family, CancellationToken ct);
    }
}
