namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One vertex of an advanced motion zone. Ring's vertex1..vertex8 keys map to Position 1..8.</summary>
    public class DeviceAdvancedMotionZoneVertex
    {
        public Guid Id { get; set; }
        public Guid DeviceAdvancedMotionZoneId { get; set; }
        /// <summary>1-based vertex number as Ring names it.</summary>
        public int Position { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
    }
}
