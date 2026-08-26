namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An append-only record of evidence export operations.</summary>
    public class ExportRecord
    {
        public Guid Id { get; set; }
        public DateTime ExportedAtUtc { get; set; }
        public required string ExportedByUserName { get; set; }
        public string? CaseReference { get; set; }
        public string? RecipientDescription { get; set; }
        public required string ArchiveFileName { get; set; }
        public required string ArchiveSha256Hash { get; set; }
        public bool WasEncrypted { get; set; }
        public int ItemCount { get; set; }
        public required string AppVersion { get; set; }
    }
}
