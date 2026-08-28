namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Generic cache-aware data accessor pattern: check cache, query API if stale.</summary>
    public interface ICacheableDataAccessor<T> where T : class
    {
        /// <summary>Gets entity from cache if fresh, otherwise queries API and updates cache.</summary>
        /// <param name="id">Entity ID</param>
        /// <param name="maxAgeMinutes">Maximum age in minutes before cache is considered stale</param>
        Task<T?> GetOrFetchAsync(Guid id, int maxAgeMinutes, CancellationToken ct);

        /// <summary>Gets entity from local cache only (no API query).</summary>
        Task<T?> GetCachedAsync(Guid id, CancellationToken ct);

        /// <summary>Checks if cached entity is stale based on LastSyncedUtc.</summary>
        Task<bool> IsCacheStaleAsync(Guid id, int maxAgeMinutes, CancellationToken ct);

        /// <summary>Invalidates cache for an entity, forcing refresh on next GetOrFetch.</summary>
        Task InvalidateCacheAsync(Guid id, CancellationToken ct);

        /// <summary>Persists entity to cache (DB) and updates LastSyncedUtc.</summary>
        Task<T> PersistAsync(T entity, CancellationToken ct);
    }
}
