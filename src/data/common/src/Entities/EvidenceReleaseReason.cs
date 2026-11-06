namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Why a legal hold is released from evidence. Stored by name.</summary>
    public enum EvidenceReleaseReason
    {
        ResolvedNoLongerNeeded,
        CaseClosed,
        Other
    }
}
