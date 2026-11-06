namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Derives EventTypeLinks rows from existing detection data. Each database provider implements this, because the work
    /// is written as set-based SQL for that engine. Callers name only the provider key (for example "ring").
    /// </summary>
    /// <remarks>
    /// Rules every implementation must follow:
    /// <list type="bullet">
    /// <item>Detection links come from the provider's mapped CV values. Motion is never linked from a mapped value alone.</item>
    /// <item>Motion is a fallback: linked only when no other Detection type applies to the event.</item>
    /// <item>Reruns are idempotent: an existing link is never duplicated, and no existing link is deleted or updated.</item>
    /// </list>
    /// </remarks>
    public interface IEventTypeLinkBackfill
    {
        /// <summary>Inserts the missing derived links for every event and returns how many rows were inserted.</summary>
        /// <param name="providerKey">Provider whose CV values are mapped (for example "ring").</param>
        /// <param name="ct">Cancels the run between batches.</param>
        Task<int> RunAsync(string providerKey, CancellationToken ct);
    }
}
