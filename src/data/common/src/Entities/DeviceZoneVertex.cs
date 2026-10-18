namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One vertex of a zone_settings zone. Position is the 1-based list position.</summary>
    public class DeviceZoneVertex
    {
        public Guid Id { get; set; }
        public Guid DeviceZoneId { get; set; }
        public int Position { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
    }
}
