namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Latest snapshot timestamp of one doorbot (GET snapshots/timestamps). The unit of Ring's timestamp is not confirmed
    /// (epoch value as sent); rows are replaced per account.
    /// </summary>
    public class RingSnapshotTimestamp
    {
        public Guid Id { get; set; }
        public Guid RingAccountId { get; set; }
        public int Position { get; set; }
        public long DoorbotId { get; set; }

        /// <summary>Ring timestamps[].timestamp, as sent.</summary>
        public long? TimestampEpoch { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}
