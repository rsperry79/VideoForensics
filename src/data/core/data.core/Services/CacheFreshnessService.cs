using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Manages cache freshness logic: tracking LastSyncedUtc, SyncStatus, staleness.</summary>
    public class CacheFreshnessService
    {
        private readonly ILogger<CacheFreshnessService> _logger;

        public CacheFreshnessService(ILogger<CacheFreshnessService> logger)
        {
            _logger = logger;
        }

        /// <summary>Marks an entity as synced now and updates SyncStatus.</summary>
        public T MarkSynced<T>(T entity) where T : class
        {
            // Use reflection to set LastSyncedUtc and SyncStatus if they exist
            var lastSyncedProperty = entity.GetType().GetProperty("LastSyncedUtc");
            var syncStatusProperty = entity.GetType().GetProperty("SyncStatus");

            if (lastSyncedProperty?.CanWrite == true)
            {
                lastSyncedProperty.SetValue(entity, DateTime.UtcNow);
            }

            if (syncStatusProperty?.CanWrite == true)
            {
                syncStatusProperty.SetValue(entity, SyncStatus.Synced);
            }

            return entity;
        }

        /// <summary>Marks an entity as stale.</summary>
        public T MarkStale<T>(T entity) where T : class
        {
            var syncStatusProperty = entity.GetType().GetProperty("SyncStatus");
            if (syncStatusProperty?.CanWrite == true)
            {
                syncStatusProperty.SetValue(entity, SyncStatus.Stale);
            }
            return entity;
        }

        /// <summary>Marks an entity with an error status.</summary>
        public T MarkError<T>(T entity) where T : class
        {
            var syncStatusProperty = entity.GetType().GetProperty("SyncStatus");
            if (syncStatusProperty?.CanWrite == true)
            {
                syncStatusProperty.SetValue(entity, SyncStatus.Error);
            }
            return entity;
        }

        /// <summary>Checks if entity cache is stale based on LastSyncedUtc.</summary>
        public bool IsStale<T>(T entity, int maxAgeMinutes) where T : class
        {
            var lastSyncedProperty = entity.GetType().GetProperty("LastSyncedUtc");
            if (lastSyncedProperty?.GetValue(entity) is DateTime lastSynced)
            {
                var age = DateTime.UtcNow - lastSynced;
                return age.TotalMinutes > maxAgeMinutes;
            }

            // No LastSyncedUtc means it's never been synced (stale)
            return true;
        }

        /// <summary>Gets age in minutes since last sync.</summary>
        public int GetAgeMinutes<T>(T entity) where T : class
        {
            var lastSyncedProperty = entity.GetType().GetProperty("LastSyncedUtc");
            if (lastSyncedProperty?.GetValue(entity) is DateTime lastSynced)
            {
                return (int)Math.Ceiling((DateTime.UtcNow - lastSynced).TotalMinutes);
            }
            return int.MaxValue;
        }

        /// <summary>Computes hash of entity for change detection.</summary>
        public string ComputeHash<T>(T entity) where T : class
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entity);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hash);
        }

        /// <summary>Checks if entity hash has changed (indicating new data from API).</summary>
        public bool HasChanged<T>(T entity, string? previousHash) where T : class
        {
            if (previousHash == null)
                return true;

            var currentHash = ComputeHash(entity);
            return currentHash != previousHash;
        }
    }
}
