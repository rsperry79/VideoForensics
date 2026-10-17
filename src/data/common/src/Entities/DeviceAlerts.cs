namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Alert configuration for a device (P1 entity for provider metadata normalization).</summary>
    public class DeviceAlerts
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public string? AlertType { get; set; }

        /// <summary>Ring alert status value, e.g. the connection alert status "online". Null when Ring gave none.</summary>
        public string? AlertValue { get; set; }
        public bool? IsEnabled { get; set; }
    }
}
