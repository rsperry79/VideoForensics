namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One entry of a device's settings.motion_zones array, in Ring's order. Ring sends integers here.</summary>
    public class RingDeviceMotionZone
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        /// <summary>Zero-based index in Ring's array.</summary>
        public int Position { get; set; }
        public long? Value { get; set; }
    }
}
