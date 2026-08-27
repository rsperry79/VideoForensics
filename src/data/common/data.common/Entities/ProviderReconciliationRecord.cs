namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An append-only record of provider reconciliation findings for a device.</summary>
    public class ProviderReconciliationRecord
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public DateTime RanAtUtc { get; set; }
        public required string ProviderEventId { get; set; }
        public DiscrepancyType DiscrepancyType { get; set; }
        public string? FieldName { get; set; }
        public string? StoredValue { get; set; }
        public string? ProviderValue { get; set; }
        public string? Notes { get; set; }
    }
}
