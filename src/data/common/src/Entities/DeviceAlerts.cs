namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Alert configuration for a device (P1 entity for provider metadata normalization).</summary>
    public class DeviceAlerts
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public string? AlertType { get; set; }
        public bool? IsEnabled { get; set; }
    }
}
