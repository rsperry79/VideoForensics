namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for evidence access and modification audit trails.</summary>
    public interface IAuditTrailRepository
    {
        /// <summary>Logs evidence access for chain of custody.</summary>
        Task LogAccessAsync(Guid evidenceId, string userId, string action, DateTime accessAtUtc, CancellationToken ct);

        /// <summary>Gets full access history for evidence.</summary>
        Task<IReadOnlyList<AccessAuditLog>> GetAccessHistoryAsync(Guid evidenceId, CancellationToken ct);

        /// <summary>Gets access history for location.</summary>
        Task<IReadOnlyList<AccessAuditLog>> GetLocationAccessHistoryAsync(Guid locationId, CancellationToken ct);

        /// <summary>Verifies chain of custody (all accesses logged).</summary>
        Task<AccessAuditReport> VerifyChainOfCustodyAsync(Guid locationId, CancellationToken ct);

        /// <summary>Flags unauthorized access patterns.</summary>
        Task<IReadOnlyList<UnauthorizedAccessFlag>> FlagUnauthorizedAccessAsync(Guid locationId, CancellationToken ct);

        /// <summary>Gets export record for a location.</summary>
        Task<IReadOnlyList<ExportAuditRecord>> GetExportHistoryAsync(Guid locationId, CancellationToken ct);

        /// <summary>Verifies export integrity (all events exported unchanged).</summary>
        Task<ExportIntegrityReport> VerifyExportIntegrityAsync(Guid exportId, CancellationToken ct);

        /// <summary>Gets redaction history for location.</summary>
        Task<IReadOnlyList<RedactionAuditRecord>> GetRedactionHistoryAsync(Guid locationId, CancellationToken ct);

        /// <summary>Tracks full modification history for an event.</summary>
        Task<IReadOnlyList<ModificationAuditRecord>> TraceModificationHistoryAsync(Guid eventId, CancellationToken ct);

        /// <summary>Gets quick audit summary for compliance review.</summary>
        Task<AuditTrailSummary> GetAuditTrailSummaryAsync(Guid locationId, CancellationToken ct);

        /// <summary>Gets paginated access history.</summary>
        Task<PaginatedResult<AccessAuditLog>> GetAccessHistoryPaginatedAsync(
            Guid evidenceId, int pageNumber, int pageSize, CancellationToken ct);

        /// <summary>Gets cursor-paginated export records for streaming.</summary>
        Task<CursorPaginatedResult<ExportAuditRecord>> GetExportHistoryCursorAsync(
            Guid locationId, string? cursor, int pageSize, CancellationToken ct);
    }

    /// <summary>Access audit log entry.</summary>
    public class AccessAuditLog
    {
        public Guid Id { get; set; }
        public Guid EvidenceId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime AccessedAtUtc { get; set; }
        public string Action { get; set; } = string.Empty; // "View", "Download", "Export"
        public string IpAddress { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }

    /// <summary>Chain of custody report.</summary>
    public class AccessAuditReport
    {
        public Guid LocationId { get; set; }
        public int TotalEventsTracked { get; set; }
        public int AccessRecordsCount { get; set; }
        public bool IsComplete { get; set; }
        public List<AccessAuditLog> AllAccesses { get; set; } = new();
        public string CustodyStatus { get; set; } = "Unknown"; // "Intact", "Questionable", "Compromised"
    }

    /// <summary>Unauthorized access flag.</summary>
    public class UnauthorizedAccessFlag
    {
        public Guid EvidenceId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime AccessedAtUtc { get; set; }
        public string Action { get; set; } = string.Empty;
        public string FlagReason { get; set; } = string.Empty; // "OffHours", "UnauthorizedUser", "ExcessiveAccess"
        public int SuspicionScore { get; set; } // 1-100
    }

    /// <summary>Export audit record.</summary>
    public class ExportAuditRecord
    {
        public Guid ExportId { get; set; }
        public Guid LocationId { get; set; }
        public DateTime ExportedAtUtc { get; set; }
        public string ExportedBy { get; set; } = string.Empty;
        public int EventsExported { get; set; }
        public string ExportFormat { get; set; } = string.Empty; // "AES256Archive", "JSON", "CSV"
        public string Purpose { get; set; } = string.Empty; // "CaseFile", "CourtSubmission"
    }

    /// <summary>Export integrity report.</summary>
    public class ExportIntegrityReport
    {
        public Guid ExportId { get; set; }
        public int TotalEventsExported { get; set; }
        public int IntactEvents { get; set; }
        public int ModifiedEvents { get; set; }
        public bool IsIntact { get; set; }
        public string IntegrityStatus { get; set; } = "Unknown"; // "Intact", "Modified", "Compromised"
        public List<(Guid EventId, string Modification)> ModificationDetails { get; set; } = new();
    }

    /// <summary>Redaction audit record.</summary>
    public class RedactionAuditRecord
    {
        public Guid Id { get; set; }
        public Guid EvidenceId { get; set; }
        public DateTime RedactedAtUtc { get; set; }
        public string RedactedBy { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public string ContentRedacted { get; set; } = string.Empty; // "PII", "Address", "Phone", "Custom"
        public string JustificationNotes { get; set; } = string.Empty;
    }

    /// <summary>Modification audit record (history of changes to evidence).</summary>
    public class ModificationAuditRecord
    {
        public Guid EventId { get; set; }
        public DateTime ModifiedAtUtc { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;
        public string ModificationType { get; set; } = string.Empty; // "Redaction", "Annotation", "Metadata", "Export"
        public string ChangeSummary { get; set; } = string.Empty;
        public bool ApprovedByInvestigator { get; set; }
    }
}
