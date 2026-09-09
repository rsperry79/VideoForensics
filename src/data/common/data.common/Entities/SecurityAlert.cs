namespace VideoForensics.Data.Common.Entities
{
    /// <summary>AI security alert for a media item.</summary>
    public class SecurityAlert
    {
        public Guid Id { get; set; }
        public Guid MediaItemId { get; set; }
        public string? Severity { get; set; } = null;
        public string? AlertText { get; set; } = null;
    }
}
