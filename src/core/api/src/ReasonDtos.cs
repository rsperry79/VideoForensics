namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Why evidence was pinned. Same member names as the data-layer EvidencePinReason.</summary>
    public enum EvidencePinReasonDto
    {
        RelevantToIncident,
        RequestedByAuthority,
        Corroboration,
        Other
    }

    /// <summary>Why evidence was placed on hold. Same member names as the data-layer EvidenceHoldReason.</summary>
    public enum EvidenceHoldReasonDto
    {
        LegalHold,
        PendingReview,
        AwaitingRequest,
        Other
    }

    /// <summary>Why evidence was released from hold. Same member names as the data-layer EvidenceReleaseReason.</summary>
    public enum EvidenceReleaseReasonDto
    {
        ResolvedNoLongerNeeded,
        CaseClosed,
        Other
    }

    /// <summary>Why a media flag was dismissed. Same member names as the data-layer MediaFlagDismissReason.</summary>
    public enum MediaFlagDismissReasonDto
    {
        FalsePositive,
        CameraMaintenance,
        WeatherOrLighting,
        Duplicate,
        Other
    }
}
