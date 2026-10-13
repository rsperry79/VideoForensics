namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An append-only record of media file integrity verification.</summary>
    public class IntegrityRecord
    {
        public Guid Id { get; set; }
        public Guid MediaItemId { get; set; }
        public required string Sha256Hash { get; set; }
        public DateTime VerifiedAtUtc { get; set; }
        public bool Passed { get; set; }
        public string? FailureReason { get; set; }
        public required string VerifiedBy { get; set; }
    }
}
