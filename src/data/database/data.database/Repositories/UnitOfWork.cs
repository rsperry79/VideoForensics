using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Unit of work implementation for executing multi-entity operations atomically within a single transaction.</summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnitOfWork> _logger;

        /// <summary>Initializes a new instance of the UnitOfWork.</summary>
        public UnitOfWork(
            IDbContextFactory<VideoForensicsDbContext> factory,
            IServiceProvider serviceProvider,
            ILogger<UnitOfWork> logger)
        {
            _factory = factory;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>Executes a work function within a shared transaction context, committing on success or rolling back on exception.</summary>
        public async Task<T> ExecuteAsync<T>(Func<IUnitOfWorkContext, Task<T>> work, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            using var transaction = await db.Database.BeginTransactionAsync(ct);

            try
            {
                var context = new UnitOfWorkContext(db, _serviceProvider);
                var result = await work(context);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                _logger.LogInformation("Unit of work committed successfully");
                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Unit of work rolled back due to exception");
                throw;
            }
        }
    }

    /// <summary>Internal unit of work context providing repository instances bound to a shared transaction.</summary>
    internal class UnitOfWorkContext : IUnitOfWorkContext
    {
        private readonly VideoForensicsDbContext _db;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnitOfWork> _logger;

        public UnitOfWorkContext(VideoForensicsDbContext db, IServiceProvider serviceProvider)
        {
            _db = db;
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILogger<UnitOfWork>>();
        }

        public IUserRepository Users => new UnitOfWorkUserRepository(_db, _logger);
        public IProviderAccountRepository ProviderAccounts => new UnitOfWorkProviderAccountRepository(_db, _logger);
        public ILocationRepository Locations => new UnitOfWorkLocationRepository(_db, _logger);
        public IDeviceRepository Devices => new UnitOfWorkDeviceRepository(_db, _logger);
        public IMediaItemRepository MediaItems => new UnitOfWorkMediaItemRepository(_db, _logger);
        public IDownloadEventRepository DownloadEvents => new UnitOfWorkDownloadEventRepository(_db, _logger);
        public ICredentialRepository Credentials => new UnitOfWorkCredentialRepository(_db, _serviceProvider.GetRequiredService<ICredentialEncryptionProvider>(), _serviceProvider.GetRequiredService<ILogger<ICredentialRepository>>());
        public IActionLogRepository ActionLog => new UnitOfWorkActionLogRepository(_db, _logger);
        public IEventRepository Events => new UnitOfWorkEventRepository(_db, _logger);
        public IDeviceConfigRepository DeviceConfig => new UnitOfWorkDeviceConfigRepository(_db, _logger);
        public IAnnotationRepository Annotations => new UnitOfWorkAnnotationRepository(_db, _logger);
        public IProviderReconciliationRepository ProviderReconciliation => new UnitOfWorkProviderReconciliationRepository(_db, _logger);
        public IExportRecordRepository ExportRecords => new UnitOfWorkExportRecordRepository(_db, _logger);
    }

    // Internal repository implementations for unit of work (using fixed context, no factory pattern)
    // Note: These use synchronous LINQ since the DbContext is already created within the transaction scope

    internal class UnitOfWorkUserRepository : IUserRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkUserRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<User?> GetAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(_db.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId));

        public Task<User?> GetByProviderKeyAsync(string providerUserKey, CancellationToken ct) =>
            Task.FromResult(_db.Users.AsNoTracking().FirstOrDefault(u => u.ProviderUserKey == providerUserKey));

        public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<User>)_db.Users.AsNoTracking().ToList());

        public Task AddAsync(User user, CancellationToken ct) { _db.Users.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken ct) { _db.Users.Update(user); return Task.CompletedTask; }
        public Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null) _db.Users.Remove(user);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkProviderAccountRepository : IProviderAccountRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkProviderAccountRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult(_db.ProviderAccounts.AsNoTracking().FirstOrDefault(pa => pa.Id == accountId));

        public Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ProviderAccount>)_db.ProviderAccounts.AsNoTracking().Where(pa => pa.UserId == userId).ToList());

        public Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct) =>
            Task.FromResult(_db.ProviderAccounts.AsNoTracking().FirstOrDefault(pa => pa.UserId == userId && pa.ProviderName == providerName));

        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ProviderAccount>)_db.ProviderAccounts.AsNoTracking().ToList());

        public Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ProviderAccount>)_db.ProviderAccounts.AsNoTracking().Where(pa => pa.IsActive).ToList());

        public Task AddAsync(ProviderAccount account, CancellationToken ct) { _db.ProviderAccounts.Add(account); return Task.CompletedTask; }
        public Task UpdateAsync(ProviderAccount account, CancellationToken ct) { _db.ProviderAccounts.Update(account); return Task.CompletedTask; }
        public Task DeleteAsync(Guid accountId, CancellationToken ct)
        {
            var account = _db.ProviderAccounts.FirstOrDefault(pa => pa.Id == accountId);
            if (account != null) _db.ProviderAccounts.Remove(account);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkLocationRepository : ILocationRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkLocationRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<Location?> GetAsync(Guid locationId, CancellationToken ct) =>
            Task.FromResult(_db.Locations.AsNoTracking().FirstOrDefault(l => l.Id == locationId));

        public Task<IReadOnlyList<Location>> GetByProviderAccountIdAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Location>)_db.Locations.AsNoTracking().Where(l => l.ProviderAccountId == accountId).ToList());

        public Task<Location?> GetByProviderLocationIdAsync(Guid accountId, string providerLocationId, CancellationToken ct) =>
            Task.FromResult(_db.Locations.AsNoTracking().FirstOrDefault(l => l.ProviderAccountId == accountId && l.ProviderLocationId == providerLocationId));

        public Task<IReadOnlyList<Location>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Location>)_db.Locations.AsNoTracking().ToList());

        public Task AddAsync(Location location, CancellationToken ct) { _db.Locations.Add(location); return Task.CompletedTask; }
        public Task UpdateAsync(Location location, CancellationToken ct) { _db.Locations.Update(location); return Task.CompletedTask; }
        public Task DeleteAsync(Guid locationId, CancellationToken ct)
        {
            var location = _db.Locations.FirstOrDefault(l => l.Id == locationId);
            if (location != null) _db.Locations.Remove(location);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkDeviceRepository : IDeviceRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkDeviceRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<Device?> GetAsync(Guid deviceId, CancellationToken ct) =>
            Task.FromResult(_db.Devices.AsNoTracking().FirstOrDefault(d => d.Id == deviceId));

        public Task<IReadOnlyList<Device>> GetByLocationIdAsync(Guid locationId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Device>)_db.Devices.AsNoTracking().Where(d => d.LocationId == locationId).ToList());

        public Task<Device?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct) =>
            Task.FromResult(_db.Devices.AsNoTracking().FirstOrDefault(d => d.LocationId == locationId && d.ProviderDeviceId == providerDeviceId));

        public Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Device>)_db.Devices.AsNoTracking().ToList());

        public Task AddAsync(Device device, CancellationToken ct) { _db.Devices.Add(device); return Task.CompletedTask; }
        public Task UpdateAsync(Device device, CancellationToken ct) { _db.Devices.Update(device); return Task.CompletedTask; }
        public Task UpdateLastSuccessfulPullAsync(Guid deviceId, DateTime pulledAtUtc, CancellationToken ct)
        {
            var device = _db.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device != null) device.LastSuccessfulPullAtUtc = pulledAtUtc;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid deviceId, CancellationToken ct)
        {
            var device = _db.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device != null) _db.Devices.Remove(device);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkMediaItemRepository : IMediaItemRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkMediaItemRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct) =>
            Task.FromResult(_db.MediaItems.AsNoTracking().FirstOrDefault(m => m.Id == mediaItemId));

        public Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DeviceId == deviceId).ToList());

        public Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc).ToList());

        public Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct) =>
            Task.FromResult(_db.MediaItems.AsNoTracking().FirstOrDefault(m => m.Sha256Hash == sha256Hash));

        public Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DownloadEventId == downloadEventId).ToList());

        public Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().ToList());

        public Task AddAsync(MediaItem mediaItem, CancellationToken ct) { _db.MediaItems.Add(mediaItem); return Task.CompletedTask; }
        public Task UpdateAsync(MediaItem mediaItem, CancellationToken ct) { _db.MediaItems.Update(mediaItem); return Task.CompletedTask; }
        public Task DeleteAsync(Guid mediaItemId, CancellationToken ct)
        {
            var mediaItem = _db.MediaItems.FirstOrDefault(m => m.Id == mediaItemId);
            if (mediaItem != null) _db.MediaItems.Remove(mediaItem);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkDownloadEventRepository : IDownloadEventRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkDownloadEventRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<DownloadEvent?> GetAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult(_db.DownloadEvents.AsNoTracking().FirstOrDefault(de => de.Id == eventId));

        public Task<IReadOnlyList<DownloadEvent>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<DownloadEvent>)_db.DownloadEvents.AsNoTracking().Where(de => de.DeviceId == deviceId).ToList());

        public Task<DateTime?> GetLatestSuccessfulEventTimeAsync(Guid deviceId, CancellationToken ct)
        {
            var latest = _db.DownloadEvents.AsNoTracking()
                .Where(de => de.DeviceId == deviceId && de.Success && de.DownloadCompletedUtc.HasValue)
                .OrderByDescending(de => de.EventOccurredAtUtc)
                .FirstOrDefault();
            return Task.FromResult(latest?.EventOccurredAtUtc);
        }

        public Task<bool> ExistsForProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct) =>
            Task.FromResult(_db.DownloadEvents.AsNoTracking().Any(de => de.DeviceId == deviceId && de.ProviderEventId == providerEventId));

        public Task<DownloadEvent?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct) =>
            Task.FromResult(_db.DownloadEvents.AsNoTracking().FirstOrDefault(de => de.DeviceId == deviceId && de.ProviderEventId == providerEventId));

        public Task<IReadOnlyList<DownloadEvent>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<DownloadEvent>)_db.DownloadEvents.AsNoTracking().ToList());

        public Task AddAsync(DownloadEvent downloadEvent, CancellationToken ct) { _db.DownloadEvents.Add(downloadEvent); return Task.CompletedTask; }
        public Task UpdateAsync(DownloadEvent downloadEvent, CancellationToken ct) { _db.DownloadEvents.Update(downloadEvent); return Task.CompletedTask; }
        public Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            var downloadEvent = _db.DownloadEvents.FirstOrDefault(de => de.Id == eventId);
            if (downloadEvent != null) _db.DownloadEvents.Remove(downloadEvent);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkCredentialRepository : ICredentialRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ICredentialEncryptionProvider _encryptionProvider;
        private readonly ILogger _logger;

        public UnitOfWorkCredentialRepository(VideoForensicsDbContext db, ICredentialEncryptionProvider encryptionProvider, ILogger logger) { _db = db; _encryptionProvider = encryptionProvider; _logger = logger; }

        public async Task<(string CredentialType, string DecryptedValue)?> GetAsync(Guid providerAccountId, string credentialType, CancellationToken ct)
        {
            var credential = _db.Credentials.AsNoTracking().FirstOrDefault(c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType);
            if (credential == null) return null;
            var decryptedValue = await _encryptionProvider.DecryptAsync(credential.EncryptedValue, ct);
            return (credential.CredentialType, decryptedValue);
        }

        public Task<IReadOnlyList<Credential>> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Credential>)_db.Credentials.AsNoTracking().Where(c => c.ProviderAccountId == providerAccountId).ToList());

        public async Task SetAsync(Guid providerAccountId, string credentialType, string plainValue, CancellationToken ct)
        {
            var encryptedValue = await _encryptionProvider.EncryptAsync(plainValue, ct);
            var credential = _db.Credentials.FirstOrDefault(c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType);
            if (credential == null)
            {
                credential = new Credential { Id = Guid.NewGuid(), ProviderAccountId = providerAccountId, CredentialType = credentialType, EncryptedValue = encryptedValue, EncryptionProvider = "DataProtection", CreatedUtc = DateTime.UtcNow };
                _db.Credentials.Add(credential);
            }
            else
            {
                credential.EncryptedValue = encryptedValue;
                credential.RotatedUtc = DateTime.UtcNow;
                _db.Credentials.Update(credential);
            }
        }

        public Task DeleteAsync(Guid providerAccountId, string credentialType, CancellationToken ct)
        {
            var credential = _db.Credentials.FirstOrDefault(c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType);
            if (credential != null) _db.Credentials.Remove(credential);
            return Task.CompletedTask;
        }

        public Task DeleteByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            var credentials = _db.Credentials.Where(c => c.ProviderAccountId == providerAccountId).ToList();
            if (credentials.Count > 0) _db.Credentials.RemoveRange(credentials);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkActionLogRepository : IActionLogRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkActionLogRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ActionLogEntry?> GetAsync(Guid entryId, CancellationToken ct) =>
            Task.FromResult(_db.ActionLogEntries.AsNoTracking().FirstOrDefault(ale => ale.Id == entryId));

        /// <summary>
        /// Appends a hash-chained entry using the SAME scheme as the standalone ActionLogRepository
        /// (SHA-256 of previousHash|actor|action|entityType|entityId|timestampUtc|detailsJson), but
        /// against the shared unit-of-work DbContext/transaction instead of opening its own - the
        /// caller (UnitOfWork.ExecuteAsync) already owns the surrounding transaction and commit/rollback.
        /// </summary>
        public Task<ActionLogEntry> AppendAsync(string actor, ActorType actorType, string action, string entityType, Guid? entityId, string? detailsJson, CancellationToken ct)
        {
            var lastEntry = _db.ActionLogEntries
                .OrderByDescending(ale => ale.TimestampUtc)
                .ThenByDescending(ale => ale.Id)
                .FirstOrDefault();

            var previousEntryHash = lastEntry?.EntryHash;
            var timestampUtc = DateTime.UtcNow;

            var canonicalString = $"{previousEntryHash ?? ""}|{actor}|{action}|{entityType}|{entityId}|{timestampUtc:O}|{detailsJson ?? ""}";
            var entryHash = ComputeSha256Hash(canonicalString);

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

            _db.ActionLogEntries.Add(entry);
            return Task.FromResult(entry);
        }

        public Task<IReadOnlyList<ActionLogEntry>> GetHistoryForEntityAsync(string entityType, Guid entityId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ActionLogEntry>)_db.ActionLogEntries.AsNoTracking()
                .Where(ale => ale.EntityType == entityType && ale.EntityId == entityId)
                .OrderByDescending(ale => ale.TimestampUtc)
                .ToList());

        public Task<IReadOnlyList<ActionLogEntry>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ActionLogEntry>)_db.ActionLogEntries.AsNoTracking().ToList());

        public Task<bool> VerifyChainIntegrityAsync(CancellationToken ct)
        {
            var entries = _db.ActionLogEntries.AsNoTracking()
                .OrderBy(ale => ale.TimestampUtc)
                .ThenBy(ale => ale.Id)
                .ToList();

            string? expectedPreviousHash = null;
            foreach (var entry in entries)
            {
                if (entry.PreviousEntryHash != expectedPreviousHash)
                {
                    return Task.FromResult(false);
                }

                var canonicalString = $"{entry.PreviousEntryHash ?? ""}|{entry.Actor}|{entry.Action}|{entry.EntityType}|{entry.EntityId}|{entry.TimestampUtc:O}|{entry.DetailsJson ?? ""}";
                if (entry.EntryHash != ComputeSha256Hash(canonicalString))
                {
                    return Task.FromResult(false);
                }

                expectedPreviousHash = entry.EntryHash;
            }

            return Task.FromResult(true);
        }

        private static string ComputeSha256Hash(string input)
        {
            var inputBytes = Encoding.UTF8.GetBytes(input);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }

    internal class UnitOfWorkEventRepository : IEventRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkEventRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<Event?> GetAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult(_db.Events.AsNoTracking().FirstOrDefault(e => e.Id == eventId));

        public Task<Event?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct) =>
            Task.FromResult(_db.Events.AsNoTracking().FirstOrDefault(e => e.DeviceId == deviceId && e.ProviderEventId == providerEventId));

        public Task<Event> UpsertAsync(Event @event, CancellationToken ct)
        {
            var existing = _db.Events.FirstOrDefault(e => e.DeviceId == @event.DeviceId && e.ProviderEventId == @event.ProviderEventId);
            if (existing == null)
            {
                _db.Events.Add(@event);
                return Task.FromResult(@event);
            }
            existing.EventType = @event.EventType;
            existing.OccurredAtUtc = @event.OccurredAtUtc;
            existing.SnapshotUrl = @event.SnapshotUrl;
            existing.MetadataJson = @event.MetadataJson;
            existing.DiscoveredAtUtc = @event.DiscoveredAtUtc;
            _db.Events.Update(existing);
            return Task.FromResult(existing);
        }

        public Task<IReadOnlyList<Event>> ListByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .ToList());

        public Task<IReadOnlyList<Event>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking().Where(e => e.DeviceId == deviceId).ToList());

        public Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking().ToList());

        public Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            var @event = _db.Events.FirstOrDefault(e => e.Id == eventId);
            if (@event != null) _db.Events.Remove(@event);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkDeviceConfigRepository : IDeviceConfigRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkDeviceConfigRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<DeviceConfigSnapshot?> GetAsync(Guid snapshotId, CancellationToken ct) =>
            Task.FromResult(_db.DeviceConfigSnapshots.AsNoTracking().FirstOrDefault(dcs => dcs.Id == snapshotId));

        public Task<DeviceConfigSnapshot> AppendSnapshotAsync(DeviceConfigSnapshot snapshot, CancellationToken ct)
        {
            _db.DeviceConfigSnapshots.Add(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<DeviceConfigSnapshot?> GetLatestAsync(Guid deviceId, CancellationToken ct) =>
            Task.FromResult(_db.DeviceConfigSnapshots.AsNoTracking()
                .Where(dcs => dcs.DeviceId == deviceId)
                .OrderByDescending(dcs => dcs.CapturedAtUtc)
                .FirstOrDefault());

        public Task<IReadOnlyList<DeviceConfigSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<DeviceConfigSnapshot>)_db.DeviceConfigSnapshots.AsNoTracking()
                .Where(dcs => dcs.DeviceId == deviceId)
                .OrderByDescending(dcs => dcs.CapturedAtUtc)
                .ToList());

        public Task<IReadOnlyList<DeviceConfigSnapshot>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<DeviceConfigSnapshot>)_db.DeviceConfigSnapshots.AsNoTracking().ToList());
    }

    internal class UnitOfWorkAnnotationRepository : IAnnotationRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkAnnotationRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<Annotation?> GetAsync(Guid annotationId, CancellationToken ct) =>
            Task.FromResult(_db.Annotations.AsNoTracking().FirstOrDefault(a => a.Id == annotationId));

        public Task<Annotation> AddAsync(string entityType, Guid entityId, string source, string key, string value, CancellationToken ct)
        {
            var annotation = new Annotation { Id = Guid.NewGuid(), EntityType = entityType, EntityId = entityId, Source = source, Key = key, Value = value, CreatedAtUtc = DateTime.UtcNow };
            _db.Annotations.Add(annotation);
            return Task.FromResult(annotation);
        }

        public Task<IReadOnlyList<Annotation>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<Annotation>)_db.Annotations.AsNoTracking()
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .ToList());

        public Task<IReadOnlyList<Annotation>> SearchAsync(string key, string? value, CancellationToken ct)
        {
            var query = _db.Annotations.AsNoTracking().Where(a => a.Key == key);
            if (!string.IsNullOrEmpty(value)) query = query.Where(a => a.Value == value);
            return Task.FromResult((IReadOnlyList<Annotation>)query.ToList());
        }

        public Task DeleteAsync(Guid annotationId, CancellationToken ct)
        {
            var annotation = _db.Annotations.FirstOrDefault(a => a.Id == annotationId);
            if (annotation != null) _db.Annotations.Remove(annotation);
            return Task.CompletedTask;
        }

        public Task DeleteForEntityAsync(string entityType, Guid entityId, CancellationToken ct)
        {
            var annotations = _db.Annotations.Where(a => a.EntityType == entityType && a.EntityId == entityId).ToList();
            if (annotations.Count > 0) _db.Annotations.RemoveRange(annotations);
            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkProviderReconciliationRepository : IProviderReconciliationRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkProviderReconciliationRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ProviderReconciliationRecord?> GetAsync(Guid recordId, CancellationToken ct) =>
            Task.FromResult(_db.ProviderReconciliationRecords.AsNoTracking().FirstOrDefault(prr => prr.Id == recordId));

        public Task<ProviderReconciliationRecord> AppendAsync(ProviderReconciliationRecord record, CancellationToken ct)
        {
            _db.ProviderReconciliationRecords.Add(record);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<ProviderReconciliationRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ProviderReconciliationRecord>)_db.ProviderReconciliationRecords.AsNoTracking()
                .Where(prr => prr.DeviceId == deviceId)
                .OrderByDescending(prr => prr.RanAtUtc)
                .ToList());

        public Task<IReadOnlyList<ProviderReconciliationRecord>> GetOpenDiscrepanciesAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ProviderReconciliationRecord>)_db.ProviderReconciliationRecords.AsNoTracking()
                .OrderByDescending(prr => prr.RanAtUtc)
                .ToList());

        public Task<IReadOnlyList<ProviderReconciliationRecord>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ProviderReconciliationRecord>)_db.ProviderReconciliationRecords.AsNoTracking().ToList());
    }

    internal class UnitOfWorkExportRecordRepository : IExportRecordRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkExportRecordRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ExportRecord?> GetAsync(Guid recordId, CancellationToken ct) =>
            Task.FromResult(_db.ExportRecords.AsNoTracking().FirstOrDefault(er => er.Id == recordId));

        public Task<IReadOnlyList<ExportRecordItem>> GetItemsForRecordAsync(Guid exportRecordId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ExportRecordItem>)_db.ExportRecordItems.AsNoTracking()
                .Where(eri => eri.ExportRecordId == exportRecordId)
                .ToList());

        public Task<ExportRecord> AppendAsync(ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct)
        {
            _db.ExportRecords.Add(record);
            _db.ExportRecordItems.AddRange(items);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<ExportRecord>> GetHistoryForMediaItemAsync(Guid mediaItemId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ExportRecord>)_db.ExportRecordItems.AsNoTracking()
                .Where(eri => eri.MediaItemId == mediaItemId)
                .Select(eri => eri.ExportRecordId)
                .Distinct()
                .Join(
                    _db.ExportRecords.AsNoTracking(),
                    recordId => recordId,
                    record => record.Id,
                    (_, record) => record)
                .OrderByDescending(er => er.ExportedAtUtc)
                .ToList());

        public Task<IReadOnlyList<ExportRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ExportRecord>)_db.ExportRecords.AsNoTracking()
                .OrderByDescending(er => er.ExportedAtUtc)
                .ToList());

        public Task<IReadOnlyList<ExportRecord>> ListAsync(CancellationToken ct) =>
            Task.FromResult((IReadOnlyList<ExportRecord>)_db.ExportRecords.AsNoTracking().ToList());
    }
}
