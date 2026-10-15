namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// JSON projection of a ProviderAccountLocation junction row for backup export/import
    /// (stored in the optional <c>account_locations.json</c> backup entry).
    /// </summary>
    public class ExportedAccountLocationLink
    {
        public Guid ProviderAccountId { get; set; }
        public Guid LocationId { get; set; }
    }
}
