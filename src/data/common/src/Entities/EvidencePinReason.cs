namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Why evidence is pinned to a case. Stored by name.</summary>
    public enum EvidencePinReason
    {
        RelevantToIncident,
        RequestedByAuthority,
        Corroboration,
        Other
    }
}
