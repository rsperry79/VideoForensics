using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Models
{
    /// <summary>Report showing who accessed and exported evidence.</summary>
    public class AccessControlReport
    {
        public DateTime GeneratedAtUtc { get; set; }
        public DateTime ReportFromUtc { get; set; }
        public DateTime ReportToUtc { get; set; }
        public IReadOnlyList<AccessEvent> AccessEvents { get; set; } = new List<AccessEvent>();
        public IReadOnlyList<ExportEvent> ExportEvents { get; set; } = new List<ExportEvent>();

        public class AccessEvent
        {
            public DateTime AccessedAtUtc { get; set; }
            public string Actor { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public string EntityType { get; set; } = string.Empty;
            public Guid? EntityId { get; set; }
            public string? Details { get; set; }
        }

        public class ExportEvent
        {
            public DateTime ExportedAtUtc { get; set; }
            public string ExportedByUserName { get; set; } = string.Empty;
            public string? CaseReference { get; set; }
            public string? RecipientDescription { get; set; }
            public int ItemCount { get; set; }
        }
    }
}
