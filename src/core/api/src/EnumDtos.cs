namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Type of discrepancy found during provider reconciliation.</summary>
    public enum DiscrepancyTypeDto
    {
        /// <summary>The stored event no longer exists on the provider.</summary>
        MissingFromProvider,

        /// <summary>The provider's metadata for the event differs from the stored value.</summary>
        MetadataChanged,

        /// <summary>The provider has an event that is not stored locally.</summary>
        NewEventFoundOnProvider
    }

    /// <summary>What an already-paired, already-identified operator is allowed to do.</summary>
    public enum OperatorRoleDto
    {
        /// <summary>View everything, with no mutations of any kind.</summary>
        ReadOnly = 0,

        /// <summary>Read-only, plus creating annotations.</summary>
        Review = 1,

        /// <summary>Review, plus evidence lifecycle actions and day-to-day configuration.</summary>
        Admin = 2,

        /// <summary>Admin, plus changes to the server's trust and security posture.</summary>
        SuperAdmin = 3
    }

    /// <summary>Which network path a request arrived on.</summary>
    public enum NetworkTierDto
    {
        /// <summary>The request arrived on the local network.</summary>
        Local = 0,

        /// <summary>The request arrived on the wider network.</summary>
        Network = 1,

        /// <summary>The request arrived over the internet.</summary>
        Internet = 2
    }

    /// <summary>Operator-specific override for the two-factor authentication requirement.</summary>
    public enum TwoFactorRequirementOverrideDto
    {
        /// <summary>Follow the role's default two-factor policy.</summary>
        Inherit = 0,

        /// <summary>Force two-factor authentication on.</summary>
        Required = 1,

        /// <summary>Force two-factor authentication off.</summary>
        NotRequired = 2
    }

    /// <summary>The kind of record a note is attached to.</summary>
    public enum NoteTargetKindDto
    {
        /// <summary>A forensic case.</summary>
        Case,

        /// <summary>A jamming incident record.</summary>
        JammingIncident,

        /// <summary>A media flag.</summary>
        MediaFlag
    }

    /// <summary>Level of PII redaction applied to reports and output.</summary>
    public enum RedactionLevelDto
    {
        /// <summary>No redaction.</summary>
        None = 0,

        /// <summary>Light redaction.</summary>
        Light = 1,

        /// <summary>Medium redaction.</summary>
        Medium = 2,

        /// <summary>Heavy redaction.</summary>
        Heavy = 3
    }
}
