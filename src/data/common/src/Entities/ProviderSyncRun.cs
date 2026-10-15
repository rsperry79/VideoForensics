namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One row per account sync attempt (manual or initial), recording when it ran, whether it succeeded, and the
    /// failure message if it did not. Lets the account details page list past syncs.
    /// </summary>
    public class ProviderSyncRun
    {
        /// <summary>Primary key.</summary>
        public Guid Id { get; set; }

        /// <summary>The provider account this sync ran for.</summary>
        public Guid ProviderAccountId { get; set; }

        /// <summary>UTC time the sync started.</summary>
        public DateTime StartedUtc { get; set; }

        /// <summary>UTC time the sync finished (success or failure); null only if the row was written before completion.</summary>
        public DateTime? CompletedUtc { get; set; }

        /// <summary>True only when credentials restored and the event pull finished without recording an account error.</summary>
        public bool Success { get; set; }

        /// <summary>Failure message for an unsuccessful run; null when Success is true. Truncated to 1024 characters at write time.</summary>
        public string? ErrorMessage { get; set; }
    }
}
