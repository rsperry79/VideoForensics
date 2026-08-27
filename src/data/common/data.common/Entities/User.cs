namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Represents a provider account holder (e.g., Ring or Wyze user login).</summary>
    public class User
    {
        public Guid Id { get; set; }
        public required string ProviderUserKey { get; set; }
        public required string DisplayName { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
