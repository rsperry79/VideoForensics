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
        private readonly ILogger<UnitOfWork> _logger;
        private readonly TimeSpan _slowTransactionThreshold;
        private readonly InFlightTransactions _inFlight;
        private readonly TimeSpan _queueTimeout;

        /// <summary>
        /// How long a write waits for the gate before it fails. Set below SQLite's Default Timeout (5 seconds), so a queued
        /// write fails with a queue error rather than a lock error.
        /// </summary>
        private static readonly TimeSpan DefaultQueueTimeout = TimeSpan.FromSeconds(3);

        /// <summary>Initializes a new instance of the UnitOfWork.</summary>
        public UnitOfWork(
            IDbContextFactory<VideoForensicsDbContext> factory,
            IServiceProvider serviceProvider,
            ILogger<UnitOfWork> logger,
            TimeSpan? slowTransactionThreshold = null,
            InFlightTransactions? inFlight = null,
            TimeSpan? queueTimeout = null)
        {
            _factory = factory;
            _logger = logger;
            _slowTransactionThreshold = slowTransactionThreshold ?? DefaultSlowTransactionThreshold;
            _inFlight = inFlight ?? new InFlightTransactions();
            _queueTimeout = queueTimeout ?? DefaultQueueTimeout;
        }

        /// <summary>
        /// Executes a work function within a shared transaction context, committing on success or rolling back on exception.
        /// Each call logs its timing, so a slow write can be traced to the operation that ran it.
        /// </summary>
        /// <inheritdoc />
        public async Task<T> ReadAsync<T>(Func<IUnitOfWorkContext, Task<T>> read, CancellationToken ct)
        {
            // Inside a write, the read must see the write's uncommitted rows, so it uses the open context.
            VideoForensicsDbContext? ambient = AmbientUnitOfWork.DbContext;
            if (ambient is not null)
            {
                return await read(new UnitOfWorkContext(ambient, _logger));
            }

            // Why: no transaction and no append gate. A read that took either would wait behind every slow write in the process.
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            var context = new UnitOfWorkContext(db, _logger);
            return await read(context);
        }

        public async Task<T> ExecuteAsync<T>(Func<IUnitOfWorkContext, Task<T>> work, CancellationToken ct)
        {
            // Why: a nested write joins the open transaction on its context. A second connection would wait on the outer write lock.
            VideoForensicsDbContext? ambient = AmbientUnitOfWork.DbContext;
            if (ambient is not null)
            {
                return await work(new UnitOfWorkContext(ambient, _logger));
            }

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
                await EnterGateAsync(caller, ct);
                // Why: set here, not in EnterGateAsync, because an async helper's AsyncLocal write is not seen by its caller.
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

        /// <summary>
        /// Runs work on a fresh context with no transaction, under the gate. For statements SQLite refuses inside a
        /// transaction, such as VACUUM and wal_checkpoint. Must not be called from inside a unit of work.
        /// </summary>
        public async Task<T> ExecuteWithoutTransactionAsync<T>(Func<VideoForensicsDbContext, CancellationToken, Task<T>> work, CancellationToken ct)
        {
            if (AmbientUnitOfWork.DbContext is not null)
            {
                throw new InvalidOperationException("A transaction-less write cannot run inside a unit of work.");
            }

            string caller = DescribeCaller(new StackTrace(skipFrames: 1, fNeedFileInfo: false));
            using IDisposable tracked = _inFlight.Begin(caller, DateTimeOffset.UtcNow);

            bool entered = !ActionLogAppendGate.IsHeldByCurrentFlow;
            if (entered)
            {
                await EnterGateAsync(caller, ct);
                ActionLogAppendGate.IsHeldByCurrentFlow = true;
            }

            try
            {
                await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
                return await work(db, ct);
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

        /// <summary>
        /// Waits for the gate for at most the queue timeout, and logs how long the write queued. A write that times out is
        /// refused, so a stalled transaction cannot hold every caller indefinitely.
        /// </summary>
        private async Task EnterGateAsync(string caller, CancellationToken ct)
        {
            var wait = Stopwatch.StartNew();
            if (!await ActionLogAppendGate.TryWaitAsync(_queueTimeout, ct))
            {
                _logger.LogError("Write for {Caller} gave up waiting for the write queue after {TimeoutMs} ms", caller, (long)_queueTimeout.TotalMilliseconds);
                throw new TimeoutException($"Write for {caller} timed out waiting for the write queue after {(long)_queueTimeout.TotalMilliseconds} ms.");
            }

            long waitMs = (long)wait.Elapsed.TotalMilliseconds;
            if (wait.Elapsed >= _slowTransactionThreshold)
            {
                _logger.LogWarning("Write for {Caller} waited {WaitMs} ms in the write queue", caller, waitMs);
            }
            else
            {
                _logger.LogDebug("Write for {Caller} waited {WaitMs} ms in the write queue", caller, waitMs);
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
                var context = new UnitOfWorkContext(db, _logger);
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
                _logger.LogError("Unit of work rolled back due to exception: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
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
            return name.StartsWith('<') && name.IndexOf('>') > 1 ? name[1..name.IndexOf('>')] : name;
        }
    }

    /// <summary>Internal unit of work context providing repository instances bound to a shared transaction.</summary>
    /// <summary>Gives database-internal repositories the EF context of the unit of work they run in.</summary>
    internal static class UnitOfWorkContextExtensions
    {
        /// <summary>
        /// Returns the open DbContext. Only valid inside a unit of work's read or write callback, where the context is
        /// the one the unit owns.
        /// </summary>
        internal static VideoForensicsDbContext Db(this IUnitOfWorkContext context)
        {
            return context is UnitOfWorkContext unitOfWorkContext
                ? unitOfWorkContext.Db
                : throw new InvalidOperationException("The database context is only available inside a unit of work.");
        }
    }

    internal class UnitOfWorkContext : IUnitOfWorkContext
    {
        private readonly VideoForensicsDbContext _db;
        private readonly ILogger<UnitOfWork> _logger;

        public UnitOfWorkContext(VideoForensicsDbContext db, ILogger<UnitOfWork> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>The open context of this unit of work.</summary>
        internal VideoForensicsDbContext Db => _db;

        public IUserRepository Users => new UnitOfWorkUserRepository(_db, _logger);
        public IProviderAccountRepository ProviderAccounts => new UnitOfWorkProviderAccountRepository(_db, _logger);
        public ILocationRepository Locations => new UnitOfWorkLocationRepository(_db, _logger);
        public IMediaItemRepository MediaItems => new UnitOfWorkMediaItemRepository(_db, _logger);
        public IDownloadEventRepository DownloadEvents => new UnitOfWorkDownloadEventRepository(_db, _logger);
        public IEventRepository Events => new UnitOfWorkEventRepository(_db, _logger);

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
        public Task<MediaItem?> GetLatestRollupLoopAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult(_db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.MediaFormat == "mp4"
                    && m.MetadataJson != null && m.MetadataJson.Contains("\"type\":\"snapshot-rollup\""))
                .OrderByDescending(m => m.RecordedAtUtc)
                .FirstOrDefault());
        }
        public Task<IReadOnlyList<MediaItem>> GetRecentByDeviceAsync(Guid deviceId, int limit, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DeviceId == deviceId).OrderByDescending(m => m.RecordedAtUtc).Take(limit).ToList());
        }

        public Task<MediaItemPage> GetPageByDevicesAndDateRangeAsync(
            IReadOnlyCollection<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, int skip, int take, CancellationToken ct, bool videoOnly = false)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(skip, 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 500);

            Guid[] ids = deviceIds.ToArray();
            IQueryable<MediaItem> matches = _db.MediaItems
                .AsNoTracking()
                .Where(m => ids.Contains(m.DeviceId) && !m.IsPurged && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc);
            if (videoOnly)
            {
                matches = matches.Where(m => m.MediaFormat != null && (m.MediaFormat.Contains("mp4") || m.MediaFormat.Contains("video")));
            }

            int total = matches.Count();
            var items = matches.OrderBy(m => m.RecordedAtUtc).ThenBy(m => m.Id).Skip(skip).Take(take).ToList();
            return Task.FromResult(new MediaItemPage(items, total));
        }

        public Task<IReadOnlyList<MediaItem>> GetByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking().Where(m => m.DeviceId == deviceId && m.RecordedAtUtc >= fromUtc && m.RecordedAtUtc <= toUtc).ToList());
        }

        public Task<IReadOnlyList<MediaItem>> ListFlaggedSinceAsync(DateTime sinceUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking()
                .Where(m => m.RecordedAtUtc >= sinceUtc && m.RedFlags != null && m.RedFlags.Contains("BitrateCollapse"))
                .OrderBy(m => m.RecordedAtUtc)
                .ToList());
        }

        public Task<IReadOnlyList<MediaItem>> ListMissingCodecAsync(int limit, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MediaItem>)_db.MediaItems.AsNoTracking()
                .Where(m => !m.IsPurged && m.VideoCodec == null)
                .OrderBy(m => m.RecordedAtUtc)
                .Take(limit)
                .ToList());
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

        public Task<IReadOnlyList<MeasuredBitrate>> GetMeasuredBitrateByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<MeasuredBitrate>)_db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.VideoBitrateBps != null && m.BitrateEstimated != true)
                .OrderBy(m => m.RecordedAtUtc)
                .Select(m => new MeasuredBitrate(m.RecordedAtUtc, m.VideoBitrateBps!.Value, m.LightingMode, m.RedFlags))
                .ToList());
        }

        public Task<AllTimeBitrateBaseline> GetBitrateBaselineByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            // Same aggregate as MediaItemRepository: one SQL GROUP BY, with the arithmetic shared through FromTotals.
            var totals = _db.MediaItems.AsNoTracking()
                .Where(m => m.DeviceId == deviceId && m.VideoBitrateBps != null && m.BitrateEstimated != true
                    && (m.MetadataJson == null || !m.MetadataJson.Contains("\"type\":\"snapshot-rollup\"")))
                .GroupBy(m => m.DeviceId)
                .Select(g => new
                {
                    Count = g.Count(),
                    Sum = g.Sum(m => (double)m.VideoBitrateBps!.Value),
                    SumOfSquares = g.Sum(m => (double)m.VideoBitrateBps!.Value * (double)m.VideoBitrateBps!.Value),
                })
                .FirstOrDefault();
            return Task.FromResult(totals is null
                ? AllTimeBitrateBaseline.FromTotals(0, 0, 0)
                : AllTimeBitrateBaseline.FromTotals(totals.Count, totals.Sum, totals.SumOfSquares));
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

            // Download fields are written by the download path after discovery. Keep the stored value when the
            // incoming upsert has none, so a discovery re-upsert cannot erase a recorded download (same rule as EventRepository).
            existing.DownloadedAtUtc = @event.DownloadedAtUtc ?? existing.DownloadedAtUtc;
            existing.DownloadFailedAtUtc = @event.DownloadFailedAtUtc ?? existing.DownloadFailedAtUtc;
            existing.ApiSourceHash = @event.ApiSourceHash ?? existing.ApiSourceHash;
            existing.EventIntegrityHash = @event.EventIntegrityHash ?? existing.EventIntegrityHash;
            if (existing.DownloadedAtUtc.HasValue)
            {
                existing.DownloadStatus = EventDownloadStatus.Downloaded;
            }

            _ = _db.Events.Update(existing);
            return Task.FromResult(existing);
        }

        public Task<IReadOnlyList<Event>> ListByDeviceAndDateRangeAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
                .ToList());
        }

        public Task RecordRetryPassAsync(Guid eventId, int maxAttempts, CancellationToken ct)
        {
            // Tracked entity: the unit of work saves the change when the transaction commits.
            Event? evt = _db.Events.FirstOrDefault(e => e.Id == eventId);
            if (evt is null)
            {
                return Task.CompletedTask;
            }

            evt.DownloadAttempts++;
            if (evt.DownloadAttempts >= maxAttempts)
            {
                evt.DownloadStatus = EventDownloadStatus.Unavailable;
                evt.AwaitingDingId = false;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Event>> ListAwaitingDingIdAsync(Guid deviceId, int maxAttempts, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<Event>)_db.Events.AsNoTracking()
                .Where(e => e.DeviceId == deviceId && e.AwaitingDingId && e.DownloadAttempts < maxAttempts && e.DownloadStatus != EventDownloadStatus.Downloaded)
                .OrderBy(e => e.OccurredAtUtc)
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
                    && (included.Length == 0
                        || (included.Contains(e.EventType) && !(e.EventType == "motion" && _db.EventDetections.Any(d => d.EventId == e.Id && d.DetectionType != null && d.DetectionType != "motion" && d.DetectionType != "other_motion")))
                        || _db.EventDetections.Any(d => d.EventId == e.Id && d.DetectionType != null && included.Contains(d.DetectionType))));

            int total = matches.Count();
            var items = matches
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
}
