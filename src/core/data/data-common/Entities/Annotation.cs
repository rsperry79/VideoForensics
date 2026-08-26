namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A generic key-value annotation attachable to any entity for findings and metadata.</summary>
    public class Annotation
    {
        public Guid Id { get; set; }
        public required string EntityType { get; set; }
        public Guid EntityId { get; set; }
        public required string Source { get; set; }
        public required string Key { get; set; }
        public required string Value { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
