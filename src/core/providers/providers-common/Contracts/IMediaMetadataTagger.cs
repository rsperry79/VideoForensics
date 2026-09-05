namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>
    /// Embeds/reads a database record's GUID into a downloaded media file's own container metadata
    /// (independent of any JSON sidecar), so the file can still be matched back to its DB row if the
    /// sidecar is lost, renamed, or separated from the media file.
    /// </summary>
    public interface IMediaMetadataTagger
    {
        /// <summary>Tags the media file's container metadata with the given record ID. Returns false (non-fatal) on failure.</summary>
        Task<bool> TagEventIdAsync(string mediaFilePath, Guid eventId, CancellationToken ct);

        /// <summary>Reads back the record ID previously tagged via TagEventIdAsync, or null if not present/unreadable.</summary>
        Task<Guid?> ReadEventIdAsync(string mediaFilePath, CancellationToken ct);
    }
}
