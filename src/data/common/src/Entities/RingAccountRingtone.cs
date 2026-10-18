namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One chime ringtone from the ringtones list (one row per audio).</summary>
    public class RingAccountRingtone
    {
        public Guid Id { get; set; }
        public Guid RingAccountId { get; set; }
        public string? RingtoneId { get; set; }
        public string? Description { get; set; }
        public string? Kind { get; set; }
        public string? Category { get; set; }
        public bool? Available { get; set; }
        public int? Checksum { get; set; }
        public int? SampleRateKhz { get; set; }
        public string? Url { get; set; }
        public string? UrlAmz { get; set; }
        public string? UserId { get; set; }
        public List<RingAccountRingtoneDeviceKind> SupportedDeviceKinds { get; set; } = [];
    }
}
