namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Supplies a stable identifier for this machine, used as key material for local credential encryption.
    /// Returning null means "no identifier here"; the consumer then falls back to the hostname.
    /// </summary>
    public interface IMachineGuidSource
    {
        /// <summary>Returns the machine identifier, or null when none is available. Never throws.</summary>
        string? ReadMachineGuid();
    }
}
