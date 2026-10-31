namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One vertex of an ignore zone. Ring's vertexN keys map to Position N (1-based).</summary>
    public class RingDeviceIgnoreZoneVertex
    {
        public Guid Id { get; set; }
        public Guid RingDeviceIgnoreZoneId { get; set; }
        public int Position { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
    }
}
