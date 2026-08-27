namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Links a user to a specific provider account.</summary>
    public class ProviderAccount
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public required string ProviderName { get; set; }
        public DateTime LinkedUtc { get; set; }
        public DateTime? LastSuccessfulAuthUtc { get; set; }
        public bool IsActive { get; set; }
    }
}
