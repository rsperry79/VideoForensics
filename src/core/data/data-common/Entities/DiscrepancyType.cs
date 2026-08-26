namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Type of discrepancy found during provider reconciliation.</summary>
    public enum DiscrepancyType
    {
        MissingFromProvider,
        MetadataChanged,
        NewEventFoundOnProvider
    }
}
