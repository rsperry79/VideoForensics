using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Diagnostics;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Unit of work implementation for executing multi-entity operations atomically within a single transaction.</summary>
    public class UnitOfWork : IUnitOfWork
    {
        /// <summary>
        /// Transactions at or past this length log a Warning. Shorter ones log at Debug. The default is well above a normal
        /// write, so a Warning means a transaction held the database for long enough to block other writers.
        /// </summary>
        private static readonly TimeSpan DefaultSlowTransactionThreshold = TimeSpan.FromSeconds(1);

        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnitOfWork> _logger;
        private readonly TimeSpan _slowTransactionThreshold;
        private readonly InFlightTransactions _inFlight;

        /// <summary>Initializes a new instance of the UnitOfWork.</summary>
        public UnitOfWork(
            IDbContextFactory<VideoForensicsDbContext> factory,
            IServiceProvider serviceProvider,
            ILogger<UnitOfWork> logger,
            TimeSpan? slowTransactionThreshold = null,
            InFlightTransactions? inFlight = null)
        {
            _factory = factory;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _slowTransactionThreshold = slowTransactionThreshold ?? DefaultSlowTransactionThreshold;
            _inFlight = inFlight ?? new InFlightTransactions();
        }

        /// <summary>
        /// Executes a work function within a shared transaction context, committing on success or rolling back on exception.
        /// Each call logs its timing, so a slow write can be traced to the operation that ran it.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<IUnitOfWorkContext, Task<T>> work, CancellationToken ct)
        {
            // Read the caller before the first await: its frames are only on the stack until then.
            string caller = DescribeCaller(new StackTrace(skipFrames: 1, fNeedFileInfo: false));
            var clock = Stopwatch.StartNew();
            // Open from here until the method returns, so a stalled watchdog can name it. Waiting for the chain gate counts as in flight.
            using IDisposable tracked = _inFlight.Begin(caller, DateTimeOffset.UtcNow);

            // Hold the chain gate for the whole transaction. A chain append must not read a tail that this transaction is
            // about to extend, and no other writer may read that tail before this one commits.
            bool entered = !ActionLogAppendGate.IsHeldByCurrentFlow;
            if (entered)
            {
                await ActionLogAppendGate.WaitAsync(ct);
                ActionLogAppendGate.IsHeldByCurrentFlow = true;
            }

            try
            {
                return await RunTransactionAsync(work, caller, clock, ct);
            }
            finally
            {
                if (entered)
                {
                    ActionLogAppendGate.IsHeldByCurrentFlow = false;
                    ActionLogAppendGate.Release();
                }
            }
        }

        private async Task<T> RunTransactionAsync<T>(Func<IUnitOfWorkContext, Task<T>> work, string caller, Stopwatch clock, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
            TimeSpan begun = clock.Elapsed;
            TimeSpan worked = begun;

            // Standalone appends made inside the work use this context, so they join this transaction.
            VideoForensicsDbContext? outerDb = AmbientUnitOfWork.DbContext;
            AmbientUnitOfWork.DbContext = db;
            try
            {
                var context = new UnitOfWorkContext(db, _serviceProvider);
                T? result = await work(context);
                _ = await db.SaveChangesAsync(ct);
                worked = clock.Elapsed;
                await transaction.CommitAsync(ct);
                LogTransaction(caller, "committed", begun, worked, clock.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                // A failure before the work finished ends the work phase there; a failed commit counts as work.
                worked = clock.Elapsed;
                await transaction.RollbackAsync(ct);
                LogTransaction(caller, "rolled back", begun, worked, clock.Elapsed);
                _logger.LogError(ex, "Unit of work rolled back due to exception");
                throw;
            }
            finally
            {
                AmbientUnitOfWork.DbContext = outerDb;
            }
        }

        /// <summary>
        /// Logs one transaction's timing. The begin phase covers opening the connection and starting the transaction.
        /// The work phase covers the caller's lambda and SaveChanges, and the finish phase covers commit or rollback.
        /// </summary>
        private void LogTransaction(string caller, string outcome, TimeSpan begun, TimeSpan worked, TimeSpan finished)
        {
            long beginMs = (long)begun.TotalMilliseconds;
            long workMs = (long)(worked - begun).TotalMilliseconds;
            long finishMs = (long)(finished - worked).TotalMilliseconds;
            long totalMs = (long)finished.TotalMilliseconds;

            if (finished >= _slowTransactionThreshold)
            {
                _logger.LogWarning(
                    "Slow unit of work {Caller} {Outcome} in {TotalMs} ms (begin {BeginMs} ms, work {WorkMs} ms, finish {FinishMs} ms)",
                    caller, outcome, totalMs, beginMs, workMs, finishMs);
                return;
            }

            _logger.LogDebug(
                "Unit of work {Caller} {Outcome} in {TotalMs} ms (begin {BeginMs} ms, work {WorkMs} ms, finish {FinishMs} ms)",
                caller, outcome, totalMs, beginMs, workMs, finishMs);
        }

        /// <summary>
        /// Names the first caller outside this class, as "Type.Method". Async methods and lambdas compile to nested
        /// types with generated names, so the readable name is taken from those.
        /// </summary>
        private static string DescribeCaller(StackTrace trace)
        {
            foreach (StackFrame frame in trace.GetFrames() ?? [])
            {
                MethodBase? method = frame.GetMethod();
                if (method?.DeclaringType is not { } declared || declared.Namespace is null
                    || !declared.Namespace.StartsWith("VideoForensics.", StringComparison.Ordinal))
                {
                    continue;
                }

                Type owner = OwningType(declared);
                if (owner == typeof(UnitOfWork))
                {
                    continue;
                }

                return $"{owner.Name}.{MethodName(method, declared)}";
            }

            return "unknown caller";
        }

        private static Type OwningType(Type type)
        {
            Type owner = type;
            while (owner.Name.StartsWith('<') && owner.DeclaringType is { } outer)
            {
                owner = outer;
            }

            return owner;
        }

        private static string MethodName(MethodBase method, Type declared)
        {
            // An async state machine's type is "<Name>d__N" and its method is MoveNext; a lambda's method is "<Name>b__N".
            string name = method.Name == "MoveNext" && declared.Name.StartsWith('<') ? declared.Name : method.Name;
            if (name.StartsWith('<') && name.IndexOf('>') > 1)
            {
                return name[1..name.IndexOf('>')];
            }

            return name;
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
        public IDeviceSettingChangeRepository DeviceSettingChanges => new UnitOfWorkDeviceSettingChangeRepository(_db, _logger);
        public IProviderReconciliationRepository ProviderReconciliation => new UnitOfWorkProviderReconciliationRepository(_db, _logger);
        public IExportRecordRepository ExportRecords => new UnitOfWorkExportRecordRepository(_db, _logger);
        public IAccessAuditLogRepository AccessAuditLogs => new UnitOfWorkAccessAuditLogRepository(_db, _logger);
        public IExportAuditRecordRepository ExportAuditRecords => new UnitOfWorkExportAuditRecordRepository(_db, _logger);
        public IDetectionEntityProvider DetectionEntities => new DetectionEntityProvider(_db, _logger);

        public async Task SaveChangesAsync(CancellationToken ct)
        {
            _ = await _db.SaveChangesAsync(ct);
        }
    }

    // Internal repository implementations for unit of work (using fixed context, no factory pattern)
    // Note: These use synchronous LINQ since the DbContext is already created within the transaction scope

    internal class UnitOfWorkUserRepository : IUserRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkUserRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<User?> GetAsync(Guid userId, CancellationToken ct)
        {
            return Task.FromResult(_db.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId));
        }

        public Task<User?> GetByProviderKeyAsync(string providerUserKey, CancellationToken ct)
        {
            return Task.FromResult(_db.Users.AsNoTracking().FirstOrDefault(u => u.ProviderUserKey == providerUserKey));
        }

        public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<User>)_db.Users.AsNoTracking().ToList());
        }

        public Task AddAsync(User user, CancellationToken ct) { _ = _db.Users.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken ct) { _ = _db.Users.Update(user); return Task.CompletedTask; }
        public Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            User? user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                _ = _db.Users.Remove(user);
            }

            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkProviderAccountRepository : IProviderAccountRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkProviderAccountRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct)
        {
            return Task.FromResult(_db.ProviderAccounts.AsNoTracking().FirstOrDefault(pa => pa.Id == accountId));
        }

        public Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ProviderAccount>)_db.ProviderAccounts.AsNoTracking().Where(pa => pa.UserId == userId).ToList());
        }

        public Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct)
        {
            return Task.FromResult(_db.ProviderAccounts.AsNoTracking().FirstOrDefault(pa => pa.UserId == userId && pa.ProviderName == providerName));
        }

        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ProviderAccount>)_db.ProviderAccounts.AsNoTracking().ToList());
        }

        public Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ProviderAccount>)_db.ProviderAccounts.AsNoTracking().Where(pa => pa.IsActive && pa.RemovedUtc == null).ToList());
        }

        public Task<IReadOnlyList<ProviderAccount>> ListRemovedAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ProviderAccount>)_db.ProviderAccounts.AsNoTracking().Where(pa => pa.RemovedUtc != null).AsEnumerable().OrderByDescending(pa => pa.RemovedUtc).ToList());
        }

        public Task RetireAsync(Guid accountId, DateTime removedUtc, CancellationToken ct, string? stepUpToken = null)
        {
            ProviderAccount? account = _db.ProviderAccounts.FirstOrDefault(pa => pa.Id == accountId);
            if (account == null || account.RemovedUtc != null)
            {
                return Task.CompletedTask;
            }

            account.RemovedUtc = removedUtc;
            account.IsActive = false;
            _db.SyncSchedules.RemoveRange(_db.SyncSchedules.Where(s => s.ProviderAccountId == accountId));
            _db.Credentials.RemoveRange(_db.Credentials.Where(c => c.ProviderAccountId == accountId));
            return Task.CompletedTask;
        }

        public Task AddAsync(ProviderAccount account, CancellationToken ct)
        {
            _ = _db.ProviderAccounts.Add(account);
            // Same default as ProviderAccountRepository.AddAsync; this path is synchronous, so the check is too.
            if (!_db.SyncSchedules.Any(s => s.ProviderAccountId == account.Id))
            {
                _ = _db.SyncSchedules.Add(SyncSchedule.CreateDefault(account.Id));
            }

            return Task.CompletedTask;
        }
        public Task UpdateAsync(ProviderAccount account, CancellationToken ct) { _ = _db.ProviderAccounts.Update(account); return Task.CompletedTask; }
        public Task DeleteAsync(Guid accountId, CancellationToken ct, string? stepUpToken = null)
        {
            ProviderAccount? account = _db.ProviderAccounts.FirstOrDefault(pa => pa.Id == accountId);
            if (account != null)
            {
                _ = _db.ProviderAccounts.Remove(account);
            }

            return Task.CompletedTask;
        }

        public Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken)
        {
            ProviderAccount? account = _db.ProviderAccounts.FirstOrDefault(pa => pa.Id == providerAccountId);
            if (account != null)
            {
                account.LastErrorUtc = DateTime.UtcNow;
                account.LastErrorMessage = errorMessage;
                _ = _db.ProviderAccounts.Update(account);
            }

            return Task.CompletedTask;
        }

        public Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken)
        {
            ProviderAccount? account = _db.ProviderAccounts.FirstOrDefault(pa => pa.Id == providerAccountId);
            if (account != null)
            {
                account.LastSuccessfulAuthUtc = DateTime.UtcNow;
                account.LastErrorUtc = null;
                account.LastErrorMessage = null;
                _ = _db.ProviderAccounts.Update(account);
            }

            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkLocationRepository : ILocationRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkLocationRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<Location?> GetAsync(Guid locationId, CancellationToken ct)
        {
            return Task.FromResult(_db.Locations.AsNoTracking().FirstOrDefault(l => l.Id == locationId));
        }

        /// <summary>Lists the provider accounts linked to a location (any provider).</summary>
        public Task<IReadOnlyList<ProviderAccount>> ListProviderAccountsForLocationAsync(Guid locationId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ProviderAccount>)_db.ProviderAccountLocations
                .AsNoTracking()
                .Where(l => l.LocationId == locationId)
                .Join(_db.ProviderAccounts.AsNoTracking(), l => l.ProviderAccountId, pa => pa.Id, (l, pa) => pa)
                .ToList());
        }

        /// <summary>Lists the locations linked to a provider account.</summary>
        public Task<IReadOnlyList<Location>> ListByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Location>)_db.ProviderAccountLocations
                .AsNoTracking()
                .Where(l => l.ProviderAccountId == providerAccountId)
                .Join(_db.Locations.AsNoTracking(), l => l.LocationId, loc => loc.Id, (l, loc) => loc)
                .ToList());
        }

        /// <summary>Links a location to a provider account; no-op if the link already exists.</summary>
        public Task LinkToProviderAccountAsync(Guid locationId, Guid providerAccountId, CancellationToken ct)
        {
            bool exists = _db.ProviderAccountLocations.Any(l => l.LocationId == locationId && l.ProviderAccountId == providerAccountId)
                || _db.ProviderAccountLocations.Local.Any(l => l.LocationId == locationId && l.ProviderAccountId == providerAccountId);
            if (!exists)
            {
                _ = _db.ProviderAccountLocations.Add(new ProviderAccountLocation { LocationId = locationId, ProviderAccountId = providerAccountId });
            }

            return Task.CompletedTask;
        }

        /// <summary>Gets a location by provider location ID (globally unique).</summary>
        public Task<Location?> GetByProviderLocationIdAsync(string providerLocationId, CancellationToken ct)
        {
            return Task.FromResult(_db.Locations.AsNoTracking().FirstOrDefault(l => l.ProviderLocationId == providerLocationId));
        }

        /// <summary>Gets a location by provider location ID (accountId parameter ignored for backward compatibility).</summary>
        public Task<Location?> GetByProviderLocationIdAsync(Guid accountId, string providerLocationId, CancellationToken ct)
        {
            // accountId is ignored since ProviderLocationId is now globally unique
            return GetByProviderLocationIdAsync(providerLocationId, ct);
        }

        /// <summary>Gets a location by API response hash for deduplication.</summary>
        public Task<Location?> GetByApiHashAsync(string apiResponseHash, CancellationToken ct)
        {
            return Task.FromResult(_db.Locations.AsNoTracking().FirstOrDefault(l => l.ApiResponseHash == apiResponseHash));
        }

        public Task<IReadOnlyList<Location>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Location>)_db.Locations.AsNoTracking().ToList());
        }

        public Task AddAsync(Location location, CancellationToken ct) { _ = _db.Locations.Add(location); return Task.CompletedTask; }
        public Task UpdateAsync(Location location, CancellationToken ct) { _ = _db.Locations.Update(location); return Task.CompletedTask; }
        public Task DeleteAsync(Guid locationId, CancellationToken ct)
        {
            Location? location = _db.Locations.FirstOrDefault(l => l.Id == locationId);
            if (location != null)
            {
                _ = _db.Locations.Remove(location);
            }

            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkDeviceRepository : IDeviceRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkDeviceRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<Device?> GetAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult(_db.Devices.AsNoTracking().FirstOrDefault(d => d.Id == deviceId));
        }

        public Task<IReadOnlyList<Device>> GetByLocationIdAsync(Guid locationId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Device>)_db.Devices.AsNoTracking().Where(d => d.LocationId == locationId).ToList());
        }

        public Task<Device?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct)
        {
            return Task.FromResult(_db.Devices.AsNoTracking().FirstOrDefault(d => d.LocationId == locationId && d.ProviderDeviceId == providerDeviceId));
        }

        /// <summary>Gets a device by API response hash for deduplication.</summary>
        public Task<Device?> GetByApiHashAsync(string apiResponseHash, CancellationToken ct)
        {
            return Task.FromResult(_db.Devices.AsNoTracking().FirstOrDefault(d => d.ApiResponseHash == apiResponseHash));
        }

        public Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct)
        {
            var devices = _db.Devices.AsNoTracking().ToList();
            var links = _db.ProviderAccountLocations
                .AsNoTracking()
                .Select(l => new { l.LocationId, l.ProviderAccount!.ProviderName })
                .AsEnumerable()
                .Select(x => (x.LocationId, x.ProviderName))
                .ToList();
            DeviceProviderNames.Assign(devices, links);
            return Task.FromResult((IReadOnlyList<Device>)devices);
        }

        public Task AddAsync(Device device, CancellationToken ct) { _ = _db.Devices.Add(device); return Task.CompletedTask; }
        public Task UpdateAsync(Device device, CancellationToken ct) { _ = _db.Devices.Update(device); return Task.CompletedTask; }
        public Task UpdateLastSuccessfulPullAsync(Guid deviceId, DateTime pulledAtUtc, CancellationToken ct)
        {
            Device? device = _db.Devices.FirstOrDefault(d => d.Id == deviceId);
            device?.LastSuccessfulPullAtUtc = pulledAtUtc;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid deviceId, CancellationToken ct)
        {
            Device? device = _db.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device != null)
            {
                _ = _db.Devices.Remove(device);
            }

            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkMediaItemRepository : IMediaItemRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkMediaItemRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<MediaItem?> GetAsync(Guid mediaItemId, CancellationToken ct)
        {
            return Task.FromResult(_db.MediaItems.AsNoTracking().FirstOrDefault(m => m.Id == mediaItemId));
        }

        public Task<IReadOnlyList<MediaItem>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DeviceId == deviceId).ToList());
        }
        public Task<IReadOnlyList<MediaItem>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DeviceId == deviceId).OrderByDescending(m => m.RecordedAtUtc).Take(limit).ToList());
        }

        public Task<MediaItemPage> GetPageByDevicesAndDateRangeAsync(
            IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(skip, 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 500);

            Guid[] ids = deviceIds.ToArray();
            IQueryable<MediaItem> matches = _db.MediaItems
                .AsNoTracking()
                .Where(m => ids.Contains(m.DeviceId) && !m.IsPurged && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc);

            int total = matches.Count();
            List<MediaItem> items = matches.OrderBy(m => m.RecordedAtUtc).ThenBy(m => m.Id).Skip(skip).Take(take).ToList();
            return Task.FromResult(new MediaItemPage(items, total));
        }

        public Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc).ToList());
        }

        public Task<IReadOnlyList<MediaItem>> ListUnanalyzedAsync(int limit, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking()
                .Where(m => !m.IsPurged && m.BaselineVersion == null)
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit)
                .ToList());
        }

        public Task<IReadOnlyList<MediaItem>> GetSnapshotsByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc
                    && m.MediaFormat == "jpg" && m.MetadataJson != null && m.MetadataJson.Contains("\"type\":\"snapshot\""))
                .OrderBy(m => m.RecordedAtUtc)
                .ToList());
        }

        public Task<MediaItem?> GetByHashAsync(string sha256Hash, CancellationToken ct)
        {
            return Task.FromResult(_db.MediaItems.AsNoTracking().FirstOrDefault(m => m.Sha256Hash == sha256Hash));
        }

        /// <summary>Gets a media item by API source hash for deduplication.</summary>
        public Task<MediaItem?> GetByApiSourceHashAsync(string apiSourceHash, CancellationToken ct)
        {
            return Task.FromResult(_db.MediaItems.AsNoTracking().FirstOrDefault(m => m.ApiSourceHash == apiSourceHash));
        }

        public Task<IReadOnlyList<MediaItem>> ListVideosWithoutThumbnailAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking()
                .Where(m => !m.IsPurged && m.ThumbnailPath == null && (m.MediaFormat.Contains("video") || m.MediaFormat.Contains("mp4")))
                .ToList());
        }

        public Task<IReadOnlyList<MediaItem>> ListVideosWithoutPreviewAsync(int limit, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking()
                .Where(m => !m.IsPurged && m.PreviewPath == null && (m.MediaFormat.Contains("video") || m.MediaFormat.Contains("mp4")))
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit)
                .ToList());
        }

        public Task<IReadOnlyList<MediaItem>> GetByDownloadEventIdAsync(Guid downloadEventId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DownloadEventId == downloadEventId).ToList());
        }

        public Task<IReadOnlyList<MediaItem>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().ToList());
        }

        public Task AddAsync(MediaItem mediaItem, CancellationToken ct) { _ = _db.MediaItems.Add(mediaItem); return Task.CompletedTask; }
        public Task UpdateAsync(MediaItem mediaItem, CancellationToken ct) { _ = _db.MediaItems.Update(mediaItem); return Task.CompletedTask; }
        public Task DeleteAsync(Guid mediaItemId, CancellationToken ct)
        {
            MediaItem? mediaItem = _db.MediaItems.FirstOrDefault(m => m.Id == mediaItemId);
            if (mediaItem != null)
            {
                _ = _db.MediaItems.Remove(mediaItem);
            }

            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkDownloadEventRepository : IDownloadEventRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkDownloadEventRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<DownloadEvent?> GetAsync(Guid eventId, CancellationToken ct)
        {
            return Task.FromResult(_db.DownloadEvents.AsNoTracking().FirstOrDefault(de => de.Id == eventId));
        }

        public Task<IReadOnlyList<DownloadEvent>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<DownloadEvent>)_db.DownloadEvents.AsNoTracking().Where(de => de.DeviceId == deviceId).ToList());
        }

        public Task<DateTime?> GetLatestSuccessfulEventTimeAsync(Guid deviceId, CancellationToken ct)
        {
            DownloadEvent? latest = _db.DownloadEvents.AsNoTracking()
                .Where(de => de.DeviceId == deviceId && de.Success && de.DownloadCompletedUtc.HasValue)
                .OrderByDescending(de => de.EventOccurredAtUtc)
                .FirstOrDefault();
            return Task.FromResult(latest?.EventOccurredAtUtc);
        }

        public Task<bool> ExistsForProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return Task.FromResult(_db.DownloadEvents.AsNoTracking().Any(de => de.DeviceId == deviceId && de.ProviderEventId == providerEventId));
        }

        public Task<DownloadEvent?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return Task.FromResult(_db.DownloadEvents.AsNoTracking().FirstOrDefault(de => de.DeviceId == deviceId && de.ProviderEventId == providerEventId));
        }

        public Task<IReadOnlyList<DownloadEvent>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<DownloadEvent>)_db.DownloadEvents.AsNoTracking().ToList());
        }

        public Task AddAsync(DownloadEvent downloadEvent, CancellationToken ct) { _ = _db.DownloadEvents.Add(downloadEvent); return Task.CompletedTask; }
        public Task UpdateAsync(DownloadEvent downloadEvent, CancellationToken ct) { _ = _db.DownloadEvents.Update(downloadEvent); return Task.CompletedTask; }
        public Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            DownloadEvent? downloadEvent = _db.DownloadEvents.FirstOrDefault(de => de.Id == eventId);
            if (downloadEvent != null)
            {
                _ = _db.DownloadEvents.Remove(downloadEvent);
            }

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
            Credential? credential = _db.Credentials.AsNoTracking().FirstOrDefault(c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType);
            if (credential == null)
            {
                return null;
            }

            string decryptedValue = await _encryptionProvider.DecryptAsync(credential.EncryptedValue, ct);
            return (credential.CredentialType, decryptedValue);
        }

        public Task<IReadOnlyList<Credential>> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Credential>)_db.Credentials.AsNoTracking().Where(c => c.ProviderAccountId == providerAccountId).ToList());
        }

        public async Task SetAsync(Guid providerAccountId, string credentialType, string plainValue, CancellationToken ct)
        {
            string encryptedValue = await _encryptionProvider.EncryptAsync(plainValue, ct);
            Credential? credential = _db.Credentials.FirstOrDefault(c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType);
            if (credential == null)
            {
                credential = new Credential { Id = Guid.NewGuid(), ProviderAccountId = providerAccountId, CredentialType = credentialType, EncryptedValue = encryptedValue, EncryptionProvider = "DataProtection", CreatedUtc = DateTime.UtcNow };
                _ = _db.Credentials.Add(credential);
            }
            else
            {
                credential.EncryptedValue = encryptedValue;
                credential.RotatedUtc = DateTime.UtcNow;
                _ = _db.Credentials.Update(credential);
            }
        }

        public Task DeleteAsync(Guid providerAccountId, string credentialType, CancellationToken ct)
        {
            Credential? credential = _db.Credentials.FirstOrDefault(c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType);
            if (credential != null)
            {
                _ = _db.Credentials.Remove(credential);
            }

            return Task.CompletedTask;
        }

        public Task DeleteByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            var credentials = _db.Credentials.Where(c => c.ProviderAccountId == providerAccountId).ToList();
            if (credentials.Count > 0)
            {
                _db.Credentials.RemoveRange(credentials);
            }

            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkActionLogRepository : IActionLogRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkActionLogRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ActionLogEntry?> GetAsync(Guid entryId, CancellationToken ct)
        {
            return Task.FromResult(_db.ActionLogEntries.AsNoTracking().FirstOrDefault(ale => ale.Id == entryId));
        }

        /// <summary>
        /// Appends a hash-chained entry using the SAME scheme as the standalone ActionLogRepository
        /// (SHA-256 of previousHash|actor|action|entityType|entityId|timestampUtc|detailsJson), but
        /// against the shared unit-of-work DbContext/transaction instead of opening its own - the
        /// caller (UnitOfWork.ExecuteAsync) already owns the surrounding transaction and commit/rollback.
        /// </summary>
        public async Task<ActionLogEntry> AppendAsync(string actor, ActorType actorType, string action, string entityType, Guid? entityId, string? detailsJson, CancellationToken ct)
        {
            // Write the entries this unit of work has already appended. The tail is read from the database, so an
            // unsaved entry is invisible to it; without this flush two appends in one unit of work chain to the same
            // predecessor and the chain forks (reconciliation auto-fix appends several entries in one unit of work).
            _ = await _db.SaveChangesAsync(ct);

            ActionLogEntry? lastEntry = await _db.ActionLogEntries
                .OrderByDescending(ale => ale.TimestampUtc)
                .ThenByDescending(ale => ale.Id)
                .FirstOrDefaultAsync(ct);

            string? previousEntryHash = lastEntry?.EntryHash;

            // Verification orders by timestamp, so each entry must be strictly later than its predecessor, or a tie would
            // fall back to the random Id order and break the chain.
            DateTime timestampUtc = DateTime.UtcNow;
            if (lastEntry != null && timestampUtc <= lastEntry.TimestampUtc)
            {
                timestampUtc = DateTime.SpecifyKind(lastEntry.TimestampUtc.AddTicks(1), DateTimeKind.Utc);
            }

            string canonicalString = $"{previousEntryHash ?? string.Empty}|{actor}|{action}|{entityType}|{entityId}|{timestampUtc:O}|{detailsJson ?? string.Empty}";
            string entryHash = ComputeSha256Hash(canonicalString);

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

            _ = _db.ActionLogEntries.Add(entry);
            return entry;
        }

        public Task<IReadOnlyList<ActionLogEntry>> GetHistoryForEntityAsync(string entityType, Guid entityId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ActionLogEntry>)_db.ActionLogEntries.AsNoTracking()
                .Where(ale => ale.EntityType == entityType && ale.EntityId == entityId)
                .OrderByDescending(ale => ale.TimestampUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ActionLogEntry>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ActionLogEntry>)_db.ActionLogEntries.AsNoTracking().ToList());
        }

        public Task<ChainIntegrityResult> VerifyChainIntegrityAsync(CancellationToken ct)
        {
            var entries = _db.ActionLogEntries.AsNoTracking()
                .OrderBy(ale => ale.TimestampUtc)
                .ThenBy(ale => ale.Id)
                .ToList();

            string? expectedPreviousHash = null;
            int checkedCount = 0;
            foreach (ActionLogEntry? entry in entries)
            {
                if (entry.PreviousEntryHash != expectedPreviousHash)
                {
                    return Task.FromResult(ChainIntegrityResult.Broken(checkedCount, ChainBreakKind.PreviousHashMismatch, entry));
                }

                // SQLite reads timestamps back as Kind=Unspecified; restore UTC so the hash matches what the appender computed.
                string timestampUtc = DateTime.SpecifyKind(entry.TimestampUtc, DateTimeKind.Utc).ToString("O");
                string canonicalString = $"{entry.PreviousEntryHash ?? string.Empty}|{entry.Actor}|{entry.Action}|{entry.EntityType}|{entry.EntityId}|{timestampUtc}|{entry.DetailsJson ?? string.Empty}";
                if (entry.EntryHash != ComputeSha256Hash(canonicalString))
                {
                    return Task.FromResult(ChainIntegrityResult.Broken(checkedCount, ChainBreakKind.EntryHashMismatch, entry));
                }

                expectedPreviousHash = entry.EntryHash;
                checkedCount++;
            }

            return Task.FromResult(ChainIntegrityResult.Valid(checkedCount));
        }

        private static string ComputeSha256Hash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }

    internal class UnitOfWorkEventRepository : IEventRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkEventRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<Event?> GetAsync(Guid eventId, CancellationToken ct)
        {
            return Task.FromResult(_db.Events.AsNoTracking().FirstOrDefault(e => e.Id == eventId));
        }

        public Task<Event?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct)
        {
            return Task.FromResult(_db.Events.AsNoTracking().FirstOrDefault(e => e.DeviceId == deviceId && e.ProviderEventId == providerEventId));
        }

        /// <summary>Gets an event by API source hash for deduplication.</summary>
        public Task<Event?> GetByApiSourceHashAsync(string apiSourceHash, CancellationToken ct)
        {
            return Task.FromResult(_db.Events.AsNoTracking().FirstOrDefault(e => e.ApiSourceHash == apiSourceHash));
        }

        public Task<Event> UpsertAsync(Event @event, CancellationToken ct)
        {
            Event? existing = _db.Events.FirstOrDefault(e => e.DeviceId == @event.DeviceId && e.ProviderEventId == @event.ProviderEventId);
            if (existing == null)
            {
                _ = _db.Events.Add(@event);
                return Task.FromResult(@event);
            }

            existing.EventType = @event.EventType;
            existing.OccurredAtUtc = @event.OccurredAtUtc;
            existing.SnapshotUrl = @event.SnapshotUrl;
            existing.MetadataJson = @event.MetadataJson;
            existing.DiscoveredAtUtc = @event.DiscoveredAtUtc;
            _ = _db.Events.Update(existing);
            return Task.FromResult(existing);
        }

        public Task<IReadOnlyList<Event>> ListByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .ToList());
        }

        public Task<DateTime?> GetEarliestOccurredAtUtcAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult(_db.Events.AsNoTracking().Where(e => e.DeviceId == deviceId).Min(e => (DateTime?)e.OccurredAtUtc));
        }

        public Task<IReadOnlyList<Event>> ListByDevicePageAsync(Guid deviceId, DateTime? afterOccurredAtUtc, Guid? afterId, int take, CancellationToken ct)
        {
            IQueryable<Event> query = _db.Events.AsNoTracking().Where(e => e.DeviceId == deviceId);
            if (afterOccurredAtUtc.HasValue)
            {
                DateTime at = afterOccurredAtUtc.Value;
                Guid id = afterId ?? Guid.Empty;
                query = query.Where(e => e.OccurredAtUtc > at || (e.OccurredAtUtc == at && e.Id.CompareTo(id) > 0));
            }

            return Task.FromResult((IReadOnlyList<Event>)query.OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id).Take(take).ToList());
        }

        public Task<IReadOnlyList<Event>> ListUnansweredOrFlaggedAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking().Where(e => e.DeviceId == deviceId).ToList());
        }

        public Task<IReadOnlyList<Event>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking().ToList());
        }

        public Task DeleteAsync(Guid eventId, CancellationToken ct)
        {
            Event? @event = _db.Events.FirstOrDefault(e => e.Id == eventId);
            if (@event != null)
            {
                _ = _db.Events.Remove(@event);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Event>> ListByLocationAndDateRangeAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking()
                .Join(_db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId && x.Event.OccurredAtUtc >= fromUtc && x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToList());
        }

        public Task<IReadOnlyList<Event>> ListByDeviceEventTypeAndDateRangeAsync(Guid deviceId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.EventType == eventType && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .ToList());
        }

        public Task<IReadOnlyList<Event>> ListByLocationEventTypeAndDateRangeAsync(Guid locationId, string eventType, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking()
                .Join(_db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId && x.Event.EventType == eventType && x.Event.OccurredAtUtc >= fromUtc && x.Event.OccurredAtUtc <= toUtc)
                .Select(x => x.Event)
                .ToList());
        }

        public Task<Dictionary<string, int>> GetEventTypeSummaryAsync(Guid locationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var summary = _db.Events.AsNoTracking()
                .Join(_db.Devices, e => e.DeviceId, d => d.Id, (e, d) => new { Event = e, Device = d })
                .Where(x => x.Device.LocationId == locationId && x.Event.OccurredAtUtc >= fromUtc && x.Event.OccurredAtUtc <= toUtc)
                .GroupBy(x => x.Event.EventType)
                .ToDictionary(g => g.Key, g => g.Count());
            return Task.FromResult(summary);
        }

        public Task<EventPage> ListPageByDevicesAndDateRangeAsync(
            IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<string>? excludeEventTypes,
            IReadOnlyCollection<string>? includeEventTypes, int skip, int take, CancellationToken ct)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(skip, 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 500);
            ArgumentOutOfRangeException.ThrowIfLessThan(deviceIds.Count, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(deviceIds.Count, 64);

            Guid[] ids = deviceIds.ToArray();
            string[] excluded = excludeEventTypes?.ToArray() ?? [];
            string[] included = includeEventTypes?.ToArray() ?? [];
            IQueryable<Event> matches = _db.Events
                .AsNoTracking()
                .Where(e => ids.Contains(e.DeviceId) && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc && !excluded.Contains(e.EventType)
                    && (included.Length == 0 || included.Contains(e.EventType)));

            int total = matches.Count();
            List<Event> items = matches
                .OrderByDescending(e => e.OccurredAtUtc)
                .ThenBy(e => e.Id)
                .Skip(skip)
                .Take(take)
                .ToList();

            return Task.FromResult(new EventPage(items, total));
        }

        public Task<PaginatedResult<Event>> ListPaginatedAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            int totalCount = _db.Events.AsNoTracking().Count();

            var items = _db.Events.AsNoTracking()
                .OrderByDescending(e => e.OccurredAtUtc)
                .ThenBy(e => e.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new PaginatedResult<Event>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Task.FromResult(result);
        }

        public Task<Event> CreateAsync(Event @event, CancellationToken ct)
        {
            _ = _db.Events.Add(@event);
            return Task.FromResult(@event);
        }

        public Task UpdateAsync(Event @event, CancellationToken ct)
        {
            Event? existing = _db.Events.FirstOrDefault(e => e.Id == @event.Id);
            if (existing != null)
            {
                existing.EventType = @event.EventType;
                existing.OccurredAtUtc = @event.OccurredAtUtc;
                existing.SnapshotUrl = @event.SnapshotUrl;
                _ = _db.Events.Update(existing);
            }

            return Task.CompletedTask;
        }

        /// <summary>Updates an event's download failure status and timestamp.</summary>
        public Task UpdateDownloadFailureAsync(Guid eventId, DateTime failureTime, CancellationToken ct)
        {
            Event? @event = _db.Events.FirstOrDefault(e => e.Id == eventId);
            if (@event != null)
            {
                @event.DownloadStatus = EventDownloadStatus.DownloadFailed;
                @event.DownloadFailedAtUtc = failureTime;
                _ = _db.Events.Update(@event);
            }

            return Task.CompletedTask;
        }
    }

    internal class UnitOfWorkDeviceSettingChangeRepository : IDeviceSettingChangeRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkDeviceSettingChangeRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        // Synchronous add on the transaction's DbContext: the rows commit with the Device update that produced them.
        public Task AddRangeAsync(IEnumerable<DeviceSettingChange> changes, CancellationToken ct)
        {
            _db.DeviceSettingChanges.AddRange(changes);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeviceSettingChange>> ListByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<DeviceSettingChange>)_db.DeviceSettingChanges.AsNoTracking()
                .Where(dsc => dsc.DeviceId == deviceId)
                .OrderByDescending(dsc => dsc.ChangedAtUtc)
                .ToList());
        }
    }

    internal class UnitOfWorkDeviceConfigRepository : IDeviceConfigRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkDeviceConfigRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<DeviceConfigSnapshot?> GetAsync(Guid snapshotId, CancellationToken ct)
        {
            return Task.FromResult(_db.DeviceConfigSnapshots.AsNoTracking().FirstOrDefault(dcs => dcs.Id == snapshotId));
        }

        public Task<DeviceConfigSnapshot> AppendSnapshotAsync(DeviceConfigSnapshot snapshot, CancellationToken ct)
        {
            _ = _db.DeviceConfigSnapshots.Add(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<DeviceConfigSnapshot?> GetLatestAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult(_db.DeviceConfigSnapshots.AsNoTracking()
                .Where(dcs => dcs.DeviceId == deviceId)
                .OrderByDescending(dcs => dcs.CapturedAtUtc)
                .FirstOrDefault());
        }

        public Task<IReadOnlyList<DeviceConfigSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<DeviceConfigSnapshot>)_db.DeviceConfigSnapshots.AsNoTracking()
                .Where(dcs => dcs.DeviceId == deviceId)
                .OrderByDescending(dcs => dcs.CapturedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<DeviceConfigSnapshot>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<DeviceConfigSnapshot>)_db.DeviceConfigSnapshots.AsNoTracking().ToList());
        }
    }

    internal class UnitOfWorkProviderReconciliationRepository : IProviderReconciliationRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkProviderReconciliationRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ProviderReconciliationRecord?> GetAsync(Guid recordId, CancellationToken ct)
        {
            return Task.FromResult(_db.ProviderReconciliationRecords.AsNoTracking().FirstOrDefault(prr => prr.Id == recordId));
        }

        public Task<ProviderReconciliationRecord> AppendAsync(ProviderReconciliationRecord record, CancellationToken ct)
        {
            _ = _db.ProviderReconciliationRecords.Add(record);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<ProviderReconciliationRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ProviderReconciliationRecord>)_db.ProviderReconciliationRecords.AsNoTracking()
                .Where(prr => prr.DeviceId == deviceId)
                .OrderByDescending(prr => prr.RanAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ProviderReconciliationRecord>> GetOpenDiscrepanciesAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ProviderReconciliationRecord>)_db.ProviderReconciliationRecords.AsNoTracking()
                .OrderByDescending(prr => prr.RanAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ProviderReconciliationRecord>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ProviderReconciliationRecord>)_db.ProviderReconciliationRecords.AsNoTracking().ToList());
        }
    }

    internal class UnitOfWorkExportRecordRepository : IExportRecordRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkExportRecordRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ExportRecord?> GetAsync(Guid recordId, CancellationToken ct)
        {
            return Task.FromResult(_db.ExportRecords.AsNoTracking().FirstOrDefault(er => er.Id == recordId));
        }

        public Task<IReadOnlyList<ExportRecordItem>> GetItemsForRecordAsync(Guid exportRecordId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ExportRecordItem>)_db.ExportRecordItems.AsNoTracking()
                .Where(eri => eri.ExportRecordId == exportRecordId)
                .ToList());
        }

        public Task<ExportRecord> AppendAsync(ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct)
        {
            _ = _db.ExportRecords.Add(record);
            _db.ExportRecordItems.AddRange(items);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<ExportRecord>> GetHistoryForMediaItemAsync(Guid mediaItemId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ExportRecord>)_db.ExportRecordItems.AsNoTracking()
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
        }

        public Task<IReadOnlyList<ExportRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ExportRecord>)_db.ExportRecords.AsNoTracking()
                .OrderByDescending(er => er.ExportedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ExportRecord>> ListAsync(CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ExportRecord>)_db.ExportRecords.AsNoTracking().ToList());
        }
    }

    internal class UnitOfWorkAccessAuditLogRepository : IAccessAuditLogRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkAccessAuditLogRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<AccessAuditLogEntity> RecordAccessAsync(
            Guid evidenceId,
            string userId,
            string action,
            string ipAddress,
            string purpose,
            CancellationToken ct)
        {
            var entry = new AccessAuditLogEntity
            {
                Id = Guid.NewGuid(),
                EvidenceId = evidenceId,
                UserId = userId,
                Action = action,
                IpAddress = ipAddress,
                Purpose = purpose,
                AccessedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
            _ = _db.AccessAuditLogs.Add(entry);
            return Task.FromResult(entry);
        }

        public Task<AccessAuditLogEntity?> GetAsync(Guid logId, CancellationToken ct)
        {
            return Task.FromResult(_db.AccessAuditLogs.AsNoTracking().FirstOrDefault(l => l.Id == logId));
        }

        public Task<IReadOnlyList<AccessAuditLogEntity>> GetForEvidenceAsync(Guid evidenceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<AccessAuditLogEntity>)_db.AccessAuditLogs.AsNoTracking()
                .Where(l => l.EvidenceId == evidenceId)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<AccessAuditLogEntity>> GetByUserAsync(string userId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<AccessAuditLogEntity>)_db.AccessAuditLogs.AsNoTracking()
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<AccessAuditLogEntity>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<AccessAuditLogEntity>)_db.AccessAuditLogs.AsNoTracking()
                .Where(l => l.AccessedAtUtc >= fromUtc && l.AccessedAtUtc <= toUtc)
                .OrderByDescending(l => l.AccessedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<AccessAuditLogEntity>> ListAsync(int skip = 0, int take = 1000, CancellationToken ct = default)
        {
            return Task.FromResult((IReadOnlyList<AccessAuditLogEntity>)_db.AccessAuditLogs.AsNoTracking()
                .OrderByDescending(l => l.AccessedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToList());
        }
    }

    internal class UnitOfWorkExportAuditRecordRepository : IExportAuditRecordRepository
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public UnitOfWorkExportAuditRecordRepository(VideoForensicsDbContext db, ILogger logger) { _db = db; _logger = logger; }

        public Task<ExportAuditRecordEntity> RecordExportAsync(
            Guid locationId,
            string exportedBy,
            int eventsExported,
            string exportFormat,
            string purpose,
            CancellationToken ct)
        {
            var record = new ExportAuditRecordEntity
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                ExportedBy = exportedBy,
                EventsExported = eventsExported,
                ExportFormat = exportFormat,
                Purpose = purpose,
                ExportedAtUtc = DateTime.UtcNow
            };
            _ = _db.ExportAuditRecords.Add(record);
            return Task.FromResult(record);
        }

        public Task<ExportAuditRecordEntity?> GetAsync(Guid recordId, CancellationToken ct)
        {
            return Task.FromResult(_db.ExportAuditRecords.AsNoTracking().FirstOrDefault(r => r.Id == recordId));
        }

        public Task<IReadOnlyList<ExportAuditRecordEntity>> GetForLocationAsync(Guid locationId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ExportAuditRecordEntity>)_db.ExportAuditRecords.AsNoTracking()
                .Where(r => r.LocationId == locationId)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ExportAuditRecordEntity>> GetByUserAsync(string userId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ExportAuditRecordEntity>)_db.ExportAuditRecords.AsNoTracking()
                .Where(r => r.ExportedBy == userId)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ExportAuditRecordEntity>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<ExportAuditRecordEntity>)_db.ExportAuditRecords.AsNoTracking()
                .Where(r => r.ExportedAtUtc >= fromUtc && r.ExportedAtUtc <= toUtc)
                .OrderByDescending(r => r.ExportedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<ExportAuditRecordEntity>> ListAsync(int skip = 0, int take = 1000, CancellationToken ct = default)
        {
            return Task.FromResult((IReadOnlyList<ExportAuditRecordEntity>)_db.ExportAuditRecords.AsNoTracking()
                .OrderByDescending(r => r.ExportedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToList());
        }

        public Task<ExportStatistics> GetStatisticsAsync(CancellationToken ct)
        {
            var records = _db.ExportAuditRecords.AsNoTracking().ToList();
            var statistics = new ExportStatistics
            {
                TotalExports = records.Count,
                TotalEventsExported = records.Sum(r => r.EventsExported),
                FirstExportAtUtc = records.Count > 0 ? records.Min(r => r.ExportedAtUtc) : null,
                LastExportAtUtc = records.Count > 0 ? records.Max(r => r.ExportedAtUtc) : null,
                UniqueExporters = records.Select(r => r.ExportedBy).Distinct().Count(),
                ExportsByFormat = records
                    .GroupBy(r => r.ExportFormat)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
            return Task.FromResult(statistics);
        }
    }

    internal class DetectionEntityProvider : IDetectionEntityProvider
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger _logger;

        public DetectionEntityProvider(VideoForensicsDbContext db, ILogger logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task AddMediaItemDetectionAsync(MediaItemDetection detection, CancellationToken ct)
        {
            if (detection != null)
            {
                _ = _db.MediaItemDetections.Add(detection);
            }

            return Task.CompletedTask;
        }

        public Task AddDetectedPersonsAsync(List<DetectedPerson> persons, CancellationToken ct)
        {
            if (persons != null && persons.Count > 0)
            {
                _db.DetectedPersons.AddRange(persons);
            }

            return Task.CompletedTask;
        }

        public Task AddDetectionTypeOccurrencesAsync(List<DetectionTypeOccurrence> occurrences, CancellationToken ct)
        {
            if (occurrences != null && occurrences.Count > 0)
            {
                _db.DetectionTypeOccurrences.AddRange(occurrences);
            }

            return Task.CompletedTask;
        }

        public Task AddEventDetectionAsync(EventDetection detection, CancellationToken ct)
        {
            if (detection != null)
            {
                _ = _db.EventDetections.Add(detection);
            }

            return Task.CompletedTask;
        }

        public async Task<Guid> UpsertEventDetectionAsync(EventDetection detection, CancellationToken ct)
        {
            // One detection row per event: key on EventId. Check unsaved rows first so two upserts in
            // the same transaction don't both insert.
            EventDetection? existing = _db.EventDetections.Local.FirstOrDefault(d => d.EventId == detection.EventId)
                ?? await _db.EventDetections.FirstOrDefaultAsync(d => d.EventId == detection.EventId, ct);

            if (existing == null)
            {
                _ = _db.EventDetections.Add(detection);
                return detection.Id;
            }

            if (!ReferenceEquals(existing, detection))
            {
                existing.PersonDetected = detection.PersonDetected;
                existing.StreamBroken = detection.StreamBroken;
                existing.DetectionType = detection.DetectionType;
                existing.FullDescription = detection.FullDescription;
                existing.ShortDescription = detection.ShortDescription;
                existing.Similarity = detection.Similarity;
                existing.Anomaly = detection.Anomaly;
                existing.Confidence = detection.Confidence;
                existing.ModelVersion = detection.ModelVersion;
            }

            // Children are re-added by the caller from the latest data, so drop the previous set.
            Guid detectionId = existing.Id;
            _db.EventDetectionZones.RemoveRange(await _db.EventDetectionZones.Where(z => z.EventDetectionId == detectionId).ToListAsync(ct));
            _db.EventDetectionTypeOccurrences.RemoveRange(await _db.EventDetectionTypeOccurrences.Where(o => o.EventDetectionId == detectionId).ToListAsync(ct));

            detection.Id = detectionId;
            return detectionId;
        }

        public async Task ClearEventAlertsAndPersonsAsync(Guid eventId, bool clearAlerts, bool clearPersons, CancellationToken ct)
        {
            if (clearAlerts)
            {
                _db.EventSecurityAlerts.RemoveRange(await _db.EventSecurityAlerts.Where(a => a.EventId == eventId).ToListAsync(ct));
            }

            if (clearPersons)
            {
                _db.EventDetectedPersons.RemoveRange(await _db.EventDetectedPersons.Where(p => p.EventId == eventId).ToListAsync(ct));
            }
        }

        public Task AddEventDetectionZonesAsync(List<EventDetectionZone> zones, CancellationToken ct)
        {
            if (zones != null && zones.Count > 0)
            {
                _db.EventDetectionZones.AddRange(zones);
            }

            return Task.CompletedTask;
        }

        public Task AddEventSecurityAlertsAsync(List<EventSecurityAlert> alerts, CancellationToken ct)
        {
            if (alerts != null && alerts.Count > 0)
            {
                _db.EventSecurityAlerts.AddRange(alerts);
            }

            return Task.CompletedTask;
        }

        public Task AddEventDetectedPersonsAsync(List<EventDetectedPerson> persons, CancellationToken ct)
        {
            if (persons != null && persons.Count > 0)
            {
                _db.EventDetectedPersons.AddRange(persons);
            }

            return Task.CompletedTask;
        }

        public Task AddEventDetectionTypeOccurrencesAsync(List<EventDetectionTypeOccurrence> occurrences, CancellationToken ct)
        {
            if (occurrences != null && occurrences.Count > 0)
            {
                _db.EventDetectionTypeOccurrences.AddRange(occurrences);
            }

            return Task.CompletedTask;
        }
    }
}
