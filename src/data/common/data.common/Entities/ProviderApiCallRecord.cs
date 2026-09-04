namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One outbound call to a provider's API (Ring today), timestamped - the raw data behind the
    /// provider API budget guard (plan §5.12). Deliberately DB-backed rather than per-process memory:
    /// console, MCP, and VideoForensics.WebApp can each independently poll the same Ring account
    /// (plan §3's stated limitation), so the budget must be tracked somewhere every host shares, not
    /// per-process, or a second running host would silently think it has the full budget to itself.
    /// </summary>
    public class ProviderApiCallRecord
    {
        public Guid Id { get; set; }
        public required string ProviderName { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}
