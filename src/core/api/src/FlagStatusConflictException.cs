namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Thrown on the client when a media flag's status or version changed since the caller read it, so the caller
    /// must reload and decide again. Carries the statuses as strings, the way the server reports them.
    /// </summary>
    public sealed class FlagStatusConflictException : Exception
    {
        /// <summary>Creates the exception for a flag whose status or version changed since it was read.</summary>
        public FlagStatusConflictException(Guid flagId, string expectedStatus, string actualStatus, string? reviewedBy, DateTime? reviewedAtUtc)
            : base($"Flag {flagId} is {actualStatus}, expected {expectedStatus}.")
        {
            FlagId = flagId;
            ExpectedStatus = expectedStatus;
            ActualStatus = actualStatus;
            ReviewedBy = reviewedBy;
            ReviewedAtUtc = reviewedAtUtc;
        }

        /// <summary>Identifier of the flag.</summary>
        public Guid FlagId { get; }
        /// <summary>Status the caller expected, as a string.</summary>
        public string ExpectedStatus { get; }
        /// <summary>Status the server reports, as a string.</summary>
        public string ActualStatus { get; }

        /// <summary>Who reviewed the flag, for the "already reviewed by X" message.</summary>
        public string? ReviewedBy { get; }

        /// <summary>When the flag was reviewed (UTC), or null if not reviewed.</summary>
        public DateTime? ReviewedAtUtc { get; }
    }
}
