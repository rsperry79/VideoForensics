namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Why a legal hold is placed on evidence. Stored by name.</summary>
    public enum EvidenceHoldReason
    {
        LegalHold,
        PendingReview,
        AwaitingRequest,
        Other
    }
}
