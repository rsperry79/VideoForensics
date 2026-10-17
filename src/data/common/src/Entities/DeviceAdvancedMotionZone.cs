namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One advanced motion zone (zone1..zone3) of a stickup cam.</summary>
    public class DeviceAdvancedMotionZone
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        /// <summary>Ring's key for the zone: zone1, zone2 or zone3.</summary>
        public string ZoneKey { get; set; } = string.Empty;
        public string? Name { get; set; }
        public int? State { get; set; }
        public List<DeviceAdvancedMotionZoneVertex> Vertices { get; set; } = [];
    }
}
