namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One ignore zone (ignore_zones.zoneN) of a doorbot or stickup cam. Replaced as a set on each sync.</summary>
    public class RingDeviceIgnoreZone
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        /// <summary>N of Ring's zoneN key (1 to 4 in the captured payloads).</summary>
        public int ZoneNumber { get; set; }
        public string? Name { get; set; }
        public int? State { get; set; }
        public List<RingDeviceIgnoreZoneVertex> Vertices { get; set; } = [];
    }
}
