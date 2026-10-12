namespace VideoForensics.Data.Common.Entities
{
    /// <summary>AI security alert for an event.</summary>
    public class EventSecurityAlert
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public string? Severity { get; set; } = null;
        public string? AlertText { get; set; } = null;
    }
}
