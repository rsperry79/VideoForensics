using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>A snapshot capture can be pinned to a case like an event or a media item.</summary>
    public class CaseSnapshotItem_Tests : IAsyncLifetime
    {
        private static readonly DateTimeOffset T0 = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

        private SqliteInMemoryFixture _fixture = null!;
        private ILoggerFactory _loggerFactory = null!;
        private CaseRepository _cases = null!;
        private SnapshotCaptureRepository _snapshots = null!;
        private LocationRepository _locations = null!;
        private DeviceRepository _devices = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b => { });
            var actionLog = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
            var unitOfWork = new UnitOfWork(_fixture.Factory, new TestServiceProvider(_fixture, _loggerFactory), _loggerFactory.CreateLogger<UnitOfWork>());
            _cases = new CaseRepository(unitOfWork, actionLog, _loggerFactory.CreateLogger<CaseRepository>());
            _snapshots = new SnapshotCaptureRepository(unitOfWork,_loggerFactory.CreateLogger<SnapshotCaptureRepository>());
            _locations = new LocationRepository(new UnitOfWork(_fixture.Factory, new Repositories.TestServiceProvider(_fixture, _loggerFactory), _loggerFactory.CreateLogger<UnitOfWork>()), _loggerFactory.CreateLogger<LocationRepository>());
            _devices = new DeviceRepository(_fixture.Factory, _loggerFactory.CreateLogger<DeviceRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            _loggerFactory.Dispose();
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private async Task<SnapshotCapture> SeedSnapshotAsync()
        {
            Location location = TestDataBuilder.BuildLocation(name: "Snap Site");
            await _locations.AddAsync(location, CancellationToken.None);
            Device device = TestDataBuilder.BuildDevice(locationId: location.Id, name: "Snap Camera");
            await _devices.AddAsync(device, CancellationToken.None);
            var capture = SnapshotCapture.Create(device.Id, T0, "fresh", 200, "Devices/x/frame.jpg", null);
            return await _snapshots.AddAsync(capture, CancellationToken.None);
        }

        private Task<ForensicCase> NewCaseAsync(string number)
        {
            return _cases.CreateAsync(number, "Title", null, null, null, null, [], "user@example.com", CancellationToken.None);
        }

        [Fact]
        public async Task AddItemAsync_Snapshot_PinsSnapshotOnly()
        {
            SnapshotCapture snapshot = await SeedSnapshotAsync();
            ForensicCase forensicCase = await NewCaseAsync("CASE-SNAP-1");

            CaseItem item = await _cases.AddItemAsync(forensicCase.Id, CaseItemKind.Snapshot, snapshot.Id, "RSSI window", "user@example.com", CancellationToken.None);

            Assert.Equal(CaseItemKind.Snapshot, item.Kind);
            Assert.Equal(snapshot.Id, item.SnapshotCaptureId);
            Assert.Null(item.EventId);
            Assert.Null(item.MediaItemId);
            Assert.Null(item.MediaSha256AtAdd);
        }

        [Fact]
        public async Task AddItemAsync_Snapshot_DuplicateActivePinThrows()
        {
            SnapshotCapture snapshot = await SeedSnapshotAsync();
            ForensicCase forensicCase = await NewCaseAsync("CASE-SNAP-2");
            _ = await _cases.AddItemAsync(forensicCase.Id, CaseItemKind.Snapshot, snapshot.Id, "first", "user@example.com", CancellationToken.None);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _cases.AddItemAsync(forensicCase.Id, CaseItemKind.Snapshot, snapshot.Id, "again", "user@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task AddItemAsync_Snapshot_RePinAfterRemovalAllowed()
        {
            SnapshotCapture snapshot = await SeedSnapshotAsync();
            ForensicCase forensicCase = await NewCaseAsync("CASE-SNAP-3");
            CaseItem first = await _cases.AddItemAsync(forensicCase.Id, CaseItemKind.Snapshot, snapshot.Id, "first", "user@example.com", CancellationToken.None);
            await _cases.RemoveItemAsync(first.Id, "user@example.com", "mistake", CancellationToken.None);

            CaseItem second = await _cases.AddItemAsync(forensicCase.Id, CaseItemKind.Snapshot, snapshot.Id, "again", "user@example.com", CancellationToken.None);

            Assert.NotEqual(first.Id, second.Id);
        }

        [Fact]
        public async Task ListCasesContainingAsync_Snapshot_FindsTheCase()
        {
            SnapshotCapture snapshot = await SeedSnapshotAsync();
            ForensicCase forensicCase = await NewCaseAsync("CASE-SNAP-4");
            _ = await _cases.AddItemAsync(forensicCase.Id, CaseItemKind.Snapshot, snapshot.Id, "r", "user@example.com", CancellationToken.None);

            IReadOnlyList<ForensicCase> found = await _cases.ListCasesContainingAsync(CaseItemKind.Snapshot, snapshot.Id, CancellationToken.None);

            Assert.Equal(forensicCase.Id, Assert.Single(found).Id);
        }

        [Fact]
        public async Task CaseItem_SnapshotKindWithoutSnapshotId_IsRejectedByCheckConstraint()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-SNAP-5");
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.CaseItems.Add(new CaseItem
            {
                Id = Guid.NewGuid(),
                CaseId = forensicCase.Id,
                Kind = CaseItemKind.Snapshot,
                Reason = "bad",
                AddedBy = "tester",
                AddedAtUtc = DateTime.UtcNow,
            });

            _ = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task CaseItem_SnapshotKindWithAlsoMediaId_IsRejectedByCheckConstraint()
        {
            SnapshotCapture snapshot = await SeedSnapshotAsync();
            ForensicCase forensicCase = await NewCaseAsync("CASE-SNAP-6");
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.CaseItems.Add(new CaseItem
            {
                Id = Guid.NewGuid(),
                CaseId = forensicCase.Id,
                Kind = CaseItemKind.Snapshot,
                SnapshotCaptureId = snapshot.Id,
                MediaItemId = Guid.NewGuid(),
                Reason = "bad",
                AddedBy = "tester",
                AddedAtUtc = DateTime.UtcNow,
            });

            _ = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }
}
