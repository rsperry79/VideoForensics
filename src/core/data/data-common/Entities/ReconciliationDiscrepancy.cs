namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A discrepancy found during provider reconciliation (DTO).</summary>
    public class ReconciliationDiscrepancy
    {
        public required DiscrepancyType Type { get; set; }
        public required string ProviderEventId { get; set; }
        public string? FieldName { get; set; }
        public string? StoredValue { get; set; }
        public string? ProviderValue { get; set; }
    }
}
