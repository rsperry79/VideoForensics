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
        /// <summary>Timestamp of the last batch download completion for this account (used as default start time for next download).</summary>
        public DateTime? LastDownloadTimeUtc { get; set; }
        /// <summary>Timestamp of the most recent failure surfaced to operators (e.g. credential decryption failure). Cleared on the next successful operation.</summary>
        public DateTime? LastErrorUtc { get; set; }
        /// <summary>Description of the most recent failure surfaced to operators (e.g. credential decryption failure). Cleared on the next successful operation.</summary>
        public string? LastErrorMessage { get; set; }
    }
}
