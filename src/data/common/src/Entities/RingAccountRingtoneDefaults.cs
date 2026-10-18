namespace VideoForensics.Data.Common.Entities
{
    /// <summary>The default ringtone and owner ids returned beside the ringtones list.</summary>
    public class RingAccountRingtoneDefaults
    {
        public Guid Id { get; set; }
        public Guid RingAccountId { get; set; }
        public string? DefaultDingId { get; set; }
        public string? DefaultDingUserId { get; set; }
        public string? DefaultMotionId { get; set; }
        public string? DefaultMotionUserId { get; set; }
        public string? DefaultAlarmUserId { get; set; }
        public string? DefaultChirpUserId { get; set; }
        public string? DefaultAppstoreUserId { get; set; }
    }
}
