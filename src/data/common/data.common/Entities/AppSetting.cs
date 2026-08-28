namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Application settings persisted in the database.</summary>
    public class AppSetting
    {
        public required Guid Id { get; set; }
        public required string Key { get; set; }
        public required string Value { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public Guid? ActiveProviderAccountId { get; set; }
    }
}
