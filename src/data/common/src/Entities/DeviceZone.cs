namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One motion or package zone from a device's zone_settings.</summary>
    public class DeviceZone
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        /// <summary>Which zone_settings array the zone came from: "motion" or "package".</summary>
        public string Source { get; set; } = string.Empty;
        /// <summary>Ring's zone id. Ring sends it as a string or number; stored as text.</summary>
        public string? ZoneId { get; set; }
        public string? Name { get; set; }
        public int? State { get; set; }
        public string? Style { get; set; }
        public List<DeviceZoneDetectionType> DetectionTypes { get; set; } = [];
        public List<DeviceZoneVertex> Vertices { get; set; } = [];
    }
}
