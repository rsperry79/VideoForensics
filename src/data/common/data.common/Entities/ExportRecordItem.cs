namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A media item included in an export record.</summary>
    public class ExportRecordItem
    {
        public Guid Id { get; set; }
        public Guid ExportRecordId { get; set; }
        public Guid MediaItemId { get; set; }
        public required string MediaItemSha256HashAtExport { get; set; }
    }
}
