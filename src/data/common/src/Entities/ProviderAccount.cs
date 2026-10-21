namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Links a user to a specific provider account.</summary>
    public class ProviderAccount
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? OperatorId { get; set; }
        public required string ProviderName { get; set; }
        public DateTime LinkedUtc { get; set; }
        public DateTime? LastSuccessfulAuthUtc { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Timestamp of the last batch download completion for this account (used as default start time for next download).</summary>
        public DateTime? LastDownloadTimeUtc { get; set; }
        /// <summary>Timestamp of when the last media collection (videos or snapshots) finished successfully. Used by Collect.razor to set default start date for next collection. Nullable because first-time accounts have no history.</summary>
        public DateTime? LastCollectCompleteUtc { get; set; }
        /// <summary>Timestamp of the most recent failure surfaced to operators (e.g. credential decryption failure). Cleared on the next successful operation.</summary>
        public DateTime? LastErrorUtc { get; set; }
        /// <summary>Description of the most recent failure surfaced to operators (e.g. credential decryption failure). Cleared on the next successful operation.</summary>
        public string? LastErrorMessage { get; set; }
        /// <summary>Non-null once the account was removed: data retained, no longer active or scheduled.</summary>
        public DateTime? RemovedUtc { get; set; }
        /// <summary>
        /// True once the account's first sync finished (discovery, config and media validation). Defaults to false for new
        /// accounts so an interrupted first sync is retried; the migration marks existing accounts true so they do not re-validate.
        /// </summary>
        public bool InitialSyncCompleted { get; set; }

        /// <summary>
        /// When the last successful sync finished. Display only (Account details, Sync tab). Kept separate from
        /// <see cref="LastDownloadTimeUtc"/>, which is the legacy video-download watermark that sync must never move.
        /// </summary>
        public DateTime? LastSyncCompletedUtc { get; set; }
    }
}
