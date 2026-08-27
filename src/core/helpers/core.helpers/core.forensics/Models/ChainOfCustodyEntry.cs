using System;

namespace VideoForensics.Forensics.Models
{
    public class ChainOfCustodyEntry
    {
        public string EntryId { get; set; } = Guid.NewGuid().ToString();
        public string EvidenceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Handler { get; set; }
        public string? Action { get; set; }
        public string? Notes { get; set; }

        public string? UserRole { get; set; }
        public string? AccessReason { get; set; }
        public bool AccessApproved { get; set; } = true;
        public string? AccessRejectionReason { get; set; }
        public bool WasSuspiciousAttempt { get; set; }

        public string? PreviousEntryHash { get; set; }
        public string? EntryHash { get; set; }
        public DateTime DatetimeCreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
