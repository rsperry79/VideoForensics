using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for hash-chained ActionLogEntry entities.</summary>
    public class ActionLogRepository : IActionLogRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<ActionLogRepository> _logger;

        /// <summary>Initializes a new instance of the ActionLogRepository.</summary>
        public ActionLogRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<ActionLogRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets an action log entry by ID.</summary>
        public async Task<ActionLogEntry?> GetAsync(Guid entryId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ActionLogEntries.FirstOrDefaultAsync(ale => ale.Id == entryId, ct);
        }

        /// <summary>
        /// Appends a new entry to the hash chain. The repository - not the caller - reads the last
        /// entry's hash and computes this entry's PreviousEntryHash/EntryHash internally.
        /// Must be atomic: read the last entry hash, compute the new hash, and insert the row as a
        /// single transaction inside the repository implementation.
        ///
        /// Hash chain scheme: SHA-256 of the canonical serialization:
        /// previousHash|actor|action|entityType|entityId|timestampUtc|detailsJson
        /// where previousHash is the EntryHash of the last entry (or empty string if this is the first entry).
        /// </summary>
        public async Task<ActionLogEntry> AppendAsync(
            string actor,
            ActorType actorType,
            string action,
            string entityType,
            Guid? entityId,
            string? detailsJson,
            CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                using var transaction = await db.Database.BeginTransactionAsync(ct);

                // Read the last entry's hash atomically within this transaction
                var lastEntry = await db.ActionLogEntries
                    .OrderByDescending(ale => ale.TimestampUtc)
                    .ThenByDescending(ale => ale.Id)
                    .FirstOrDefaultAsync(ct);

                var previousEntryHash = lastEntry?.EntryHash;
                var timestampUtc = DateTime.UtcNow;

                // Compute the new entry's hash
                var canonicalString = $"{previousEntryHash ?? ""}|{actor}|{action}|{entityType}|{entityId}|{timestampUtc:O}|{detailsJson ?? ""}";
                var entryHash = ComputeSha256Hash(canonicalString);

                // Create and insert the new entry
                var entry = new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = actor,
                    ActorType = actorType,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    DetailsJson = detailsJson,
                    TimestampUtc = timestampUtc,
                    PreviousEntryHash = previousEntryHash,
                    EntryHash = entryHash
                };

                db.ActionLogEntries.Add(entry);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Action log entry appended: {EntryId} (action: {Action}, entity: {EntityType}:{EntityId})",
                    entry.Id, action, entityType, entityId);

                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending action log entry: {Action}", action);
                throw;
            }
        }

        /// <summary>Gets the history of actions for a specific entity.</summary>
        public async Task<IReadOnlyList<ActionLogEntry>> GetHistoryForEntityAsync(string entityType, Guid entityId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ActionLogEntries
                .Where(ale => ale.EntityType == entityType && ale.EntityId == entityId)
                .OrderByDescending(ale => ale.TimestampUtc)
                .ToListAsync(ct);
        }

        /// <summary>Gets all action log entries.</summary>
        public async Task<IReadOnlyList<ActionLogEntry>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.ActionLogEntries.ToListAsync(ct);
        }

        /// <summary>Verifies the integrity of the hash chain, returning true if the chain is valid.</summary>
        public async Task<bool> VerifyChainIntegrityAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var entries = await db.ActionLogEntries
                .OrderBy(ale => ale.TimestampUtc)
                .ThenBy(ale => ale.Id)
                .ToListAsync(ct);

            if (entries.Count == 0)
                return true;

            // Verify the chain starting from the first entry
            string? expectedPreviousHash = null;

            foreach (var entry in entries)
            {
                // Check that PreviousEntryHash matches what we expect
                if (entry.PreviousEntryHash != expectedPreviousHash)
                {
                    _logger.LogError("Hash chain integrity violation at entry {EntryId}: expected previous hash {ExpectedHash}, got {ActualHash}",
                        entry.Id, expectedPreviousHash, entry.PreviousEntryHash);
                    return false;
                }

                // Recompute the entry's hash to verify it's correct
                var canonicalString = $"{entry.PreviousEntryHash ?? ""}|{entry.Actor}|{entry.Action}|{entry.EntityType}|{entry.EntityId}|{entry.TimestampUtc:O}|{entry.DetailsJson ?? ""}";
                var computedHash = ComputeSha256Hash(canonicalString);

                if (entry.EntryHash != computedHash)
                {
                    _logger.LogError("Hash chain integrity violation at entry {EntryId}: expected hash {ExpectedHash}, computed {ComputedHash}",
                        entry.Id, entry.EntryHash, computedHash);
                    return false;
                }

                expectedPreviousHash = entry.EntryHash;
            }

            return true;
        }

        /// <summary>Computes a SHA-256 hash of the canonical string.</summary>
        private static string ComputeSha256Hash(string input)
        {
            var inputBytes = Encoding.UTF8.GetBytes(input);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
