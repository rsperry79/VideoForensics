namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Supplies the external Syncfusion license key, the admin override that wins over the configured and baked-in keys.
    /// The file location is platform-specific, so each platform supplies its own source; Client.Core ships the file-backed one.
    /// </summary>
    public interface ILicenseKeySource
    {
        /// <summary>
        /// Returns the trimmed license key, or null when no key is available. Blank content counts as absent.
        /// Never throws for a missing file; the registrar falls through to the next source.
        /// </summary>
        string? ReadKey();
    }
}
