namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A hold that exempts a media item from retention-policy auto-deletion until released.</summary>
    public class LegalHold
    {
        public Guid Id { get; set; }
        public Guid MediaItemId { get; set; }
        public required string Reason { get; set; }
        public required string CreatedBy { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? ReleasedBy { get; set; }
        public DateTime? ReleasedAtUtc { get; set; }
        public string? ReleaseReason { get; set; }
    }
}
