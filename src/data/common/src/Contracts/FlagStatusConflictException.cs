using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Thrown when a flag's status or version changed since the caller read it, so the caller must reload and decide again.</summary>
    public sealed class FlagStatusConflictException : Exception
    {
        public FlagStatusConflictException(Guid flagId, MediaFlagStatus expectedStatus, MediaFlagStatus actualStatus, string? reviewedBy, DateTime? reviewedAtUtc)
            : base($"Flag {flagId} is {actualStatus}, expected {expectedStatus}.")
        {
            FlagId = flagId;
            ExpectedStatus = expectedStatus;
            ActualStatus = actualStatus;
            ReviewedBy = reviewedBy;
            ReviewedAtUtc = reviewedAtUtc;
        }

        public Guid FlagId { get; }
        public MediaFlagStatus ExpectedStatus { get; }
        public MediaFlagStatus ActualStatus { get; }

        /// <summary>Who reviewed the flag, for the "already reviewed by X" message.</summary>
        public string? ReviewedBy { get; }

        public DateTime? ReviewedAtUtc { get; }
    }
}
