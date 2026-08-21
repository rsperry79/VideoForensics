namespace Ring.Api.Snapshots.Metadata.Models
{
    /// <summary>
    /// Status of metadata processing for a snapshot.
    /// </summary>
    public enum MetadataStatus
    {
        NotProcessed = 0,
        Valid = 1,
        Corrected = 2,
        Corrupt = 3,
        Failed = 4
    }
}
