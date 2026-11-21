namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Why evidence was pinned. Same member names as the data-layer EvidencePinReason.</summary>
    public enum EvidencePinReasonDto
    {
        /// <summary>Evidence is relevant to the incident.</summary>
        RelevantToIncident,
        /// <summary>Evidence was requested by an authority.</summary>
        RequestedByAuthority,
        /// <summary>Evidence corroborates other evidence.</summary>
        Corroboration,
        /// <summary>Another reason not listed here.</summary>
        Other
    }

    /// <summary>Why evidence was placed on hold. Same member names as the data-layer EvidenceHoldReason.</summary>
    public enum EvidenceHoldReasonDto
    {
        /// <summary>Held for legal reasons.</summary>
        LegalHold,
        /// <summary>Held pending review.</summary>
        PendingReview,
        /// <summary>Held while a request is awaited.</summary>
        AwaitingRequest,
        /// <summary>Another reason not listed here.</summary>
        Other
    }

    /// <summary>Why evidence was released from hold. Same member names as the data-layer EvidenceReleaseReason.</summary>
    public enum EvidenceReleaseReasonDto
    {
        /// <summary>The hold is resolved and the evidence is no longer needed.</summary>
        ResolvedNoLongerNeeded,
        /// <summary>The related case is closed.</summary>
        CaseClosed,
        /// <summary>Another reason not listed here.</summary>
        Other
    }

    /// <summary>Why a media flag was dismissed. Same member names as the data-layer MediaFlagDismissReason.</summary>
    public enum MediaFlagDismissReasonDto
    {
        /// <summary>The flag is a false positive.</summary>
        FalsePositive,
        /// <summary>The flag was caused by camera maintenance.</summary>
        CameraMaintenance,
        /// <summary>The flag was caused by weather or lighting.</summary>
        WeatherOrLighting,
        /// <summary>The flag duplicates another flag.</summary>
        Duplicate,
        /// <summary>Another reason not listed here.</summary>
        Other
    }
}
