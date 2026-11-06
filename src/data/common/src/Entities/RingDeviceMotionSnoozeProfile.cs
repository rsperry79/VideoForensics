namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One entry of a device's motion snooze profile array (settings.motion_settings.motion_snooze_profile), in the order Ring reports them.</summary>
    public class RingDeviceMotionSnoozeProfile
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>Zero-based index in Ring's array; preserves order.</summary>
        public int Position { get; set; }

        /// <summary>The array element (a number).</summary>
        public int? Value { get; set; }
    }
}
