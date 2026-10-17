namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One device kind a ringtone supports.</summary>
    public class RingAccountRingtoneDeviceKind
    {
        public Guid Id { get; set; }
        public Guid RingAccountRingtoneId { get; set; }
        public int Position { get; set; }
        public string DeviceKind { get; set; } = string.Empty;
    }
}
