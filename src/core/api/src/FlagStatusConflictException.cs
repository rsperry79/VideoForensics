namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Thrown on the client when a media flag's status or version changed since the caller read it, so the caller
    /// must reload and decide again. Carries the statuses as strings, the way the server reports them.
    /// </summary>
    public sealed class FlagStatusConflictException : Exception
    {
        public FlagStatusConflictException(Guid flagId, string expectedStatus, string actualStatus, string? reviewedBy, DateTime? reviewedAtUtc)
            : base($"Flag {flagId} is {actualStatus}, expected {expectedStatus}.")
        {
            FlagId = flagId;
            ExpectedStatus = expectedStatus;
            ActualStatus = actualStatus;
            ReviewedBy = reviewedBy;
            ReviewedAtUtc = reviewedAtUtc;
        }

        public Guid FlagId { get; }
        public string ExpectedStatus { get; }
        public string ActualStatus { get; }

        /// <summary>Who reviewed the flag, for the "already reviewed by X" message.</summary>
        public string? ReviewedBy { get; }

        public DateTime? ReviewedAtUtc { get; }
    }
}
