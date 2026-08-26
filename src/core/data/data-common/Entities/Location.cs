namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A location linked to a provider account.</summary>
    public class Location
    {
        public Guid Id { get; set; }
        public Guid ProviderAccountId { get; set; }
        public required string ProviderLocationId { get; set; }
        public required string Name { get; set; }
        public string? Address { get; set; }
        public string? MetadataJson { get; set; }
    }
}
