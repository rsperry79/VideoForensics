using Microsoft.EntityFrameworkCore;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Round trips the shared users, linked chime doorbots and snapshot timestamp writers on a real SQLite database:
    /// every column is written and read back, a second write replaces only its own parent's rows, a failed insert keeps
    /// the previous rows, and deleting a parent cascades to its children.
    /// </summary>
    public class RingSmallEndpointWriterTests : IAsyncLifetime
    {
        private static readonly DateTime Stamp = new(2026, 10, 31, 12, 30, 0, DateTimeKind.Utc);
        private SqliteInMemoryFixture _fixture = null!;
        private RingAccountSettingsWriter _writer = null!;
        private Guid _locationId;
        private Guid _otherLocationId;
        private Guid _ringAccountId;
        private Guid _chimeId;
        private Guid _otherChimeId;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _writer = new RingAccountSettingsWriter(_fixture.Factory);

            _locationId = Guid.NewGuid();
            _otherLocationId = Guid.NewGuid();
            _ringAccountId = Guid.NewGuid();
            _chimeId = Guid.NewGuid();
            _otherChimeId = Guid.NewGuid();

            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.Locations.Add(new Location { Id = _locationId, ProviderLocationId = "loc-small", Name = "Home" });
            _ = db.Locations.Add(new Location { Id = _otherLocationId, ProviderLocationId = "loc-other", Name = "Office" });
            _ = db.RingAccounts.Add(new RingAccount { Id = _ringAccountId, ProviderAccountId = Guid.NewGuid(), SubscriptionLevel = "unknown" });
            _ = db.Devices.Add(BuildChime(_chimeId, _locationId, "3003", "Hall Chime"));
            _ = db.Devices.Add(BuildChime(_otherChimeId, _locationId, "3004", "Den Chime"));
            _ = await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static Device BuildChime(Guid id, Guid locationId, string providerDeviceId, string name)
        {
            return new Device { Id = id, LocationId = locationId, ProviderDeviceId = providerDeviceId, Name = name, Type = "chime" };
        }

        private static RingLocationSharedUser BuildUser(Guid locationId, long providerUserId, string? email, bool? verified, params RingLocationSharedUserDevice[] devices)
        {
            return new RingLocationSharedUser
            {
                Id = Guid.NewGuid(),
                LocationId = locationId,
                ProviderUserId = providerUserId,
                Verified = verified,
                FirstName = "Sample",
                LastName = "Viewer",
                Email = email,
                ObjectType = "user",
                UpdatedAtUtc = Stamp,
                Devices = [.. devices],
            };
        }

        private static RingLocationSharedUserDevice BuildGrant(long providerDeviceId, string role, string? deviceType, params string[] permissions)
        {
            var grant = new RingLocationSharedUserDevice
            {
                Id = Guid.NewGuid(),
                Position = 0,
                ProviderDeviceId = providerDeviceId,
                DeviceType = deviceType,
                Role = role,
            };
            for (int i = 0; i < permissions.Length; i++)
            {
                grant.Permissions.Add(new RingLocationSharedUserDevicePermission
                {
                    Id = Guid.NewGuid(),
                    RingLocationSharedUserDeviceId = grant.Id,
                    Position = i,
                    Permission = permissions[i],
                });
            }

            return grant;
        }

        private static RingChimeLinkedDoorbot BuildLink(Guid chimeId, int position, long linkedDoorbotId, string? deviceId, string? kind, bool? callAlerts, bool? motionAlerts)
        {
            return new RingChimeLinkedDoorbot
            {
                Id = Guid.NewGuid(),
                ChimeDeviceId = chimeId,
                Position = position,
                LinkedDoorbotId = linkedDoorbotId,
                DeviceId = deviceId,
                Kind = kind,
                CallAlerts = callAlerts,
                MotionAlerts = motionAlerts,
                UpdatedAtUtc = Stamp,
            };
        }

        private static RingSnapshotTimestamp BuildSnapshot(Guid ringAccountId, int position, long doorbotId, long? timestampEpoch)
        {
            return new RingSnapshotTimestamp
            {
                Id = Guid.NewGuid(),
                RingAccountId = ringAccountId,
                Position = position,
                DoorbotId = doorbotId,
                TimestampEpoch = timestampEpoch,
                UpdatedAtUtc = Stamp,
            };
        }

        [Fact]
        public async Task RingLocationSharedUsersWriter_WriteThenRead_EveryColumnRoundTrips()
        {
            RingLocationSharedUser viewer = BuildUser(_locationId, 8001, "sample.viewer@example.com", true,
                BuildGrant(1001, "shared", "doorbot", "view", "edit"));
            RingLocationSharedUser guest = BuildUser(_locationId, 8002, null, null);
            await _writer.ReplaceLocationSharedUsersAsync(_locationId, [viewer, guest], CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            List<RingLocationSharedUser> stored = await read.RingLocationSharedUsers
                .Include(u => u.Devices).ThenInclude(d => d.Permissions)
                .OrderBy(u => u.ProviderUserId)
                .ToListAsync();

            Assert.Equal(2, stored.Count);
            RingLocationSharedUser storedViewer = stored[0];
            Assert.Equal(viewer.Id, storedViewer.Id);
            Assert.Equal(_locationId, storedViewer.LocationId);
            Assert.Equal(8001L, storedViewer.ProviderUserId);
            Assert.True(storedViewer.Verified);
            Assert.Equal("Sample", storedViewer.FirstName);
            Assert.Equal("Viewer", storedViewer.LastName);
            Assert.Equal("sample.viewer@example.com", storedViewer.Email);
            Assert.Equal("user", storedViewer.ObjectType);
            Assert.Equal(Stamp.Ticks, storedViewer.UpdatedAtUtc.Ticks);

            RingLocationSharedUserDevice grant = Assert.Single(storedViewer.Devices);
            Assert.Equal(viewer.Devices[0].Id, grant.Id);
            Assert.Equal(storedViewer.Id, grant.RingLocationSharedUserId);
            Assert.Equal(0, grant.Position);
            Assert.Equal(1001L, grant.ProviderDeviceId);
            Assert.Equal("doorbot", grant.DeviceType);
            Assert.Equal("shared", grant.Role);
            Assert.Equal(
                new[] { ("view", 0), ("edit", 1) },
                grant.Permissions.OrderBy(p => p.Position).Select(p => (p.Permission!, p.Position)).ToArray());
            Assert.All(grant.Permissions, p => Assert.Equal(grant.Id, p.RingLocationSharedUserDeviceId));

            RingLocationSharedUser storedGuest = stored[1];
            Assert.Equal(8002L, storedGuest.ProviderUserId);
            Assert.Null(storedGuest.Verified);
            Assert.Null(storedGuest.Email);
            Assert.Empty(storedGuest.Devices);
        }

        [Fact]
        public async Task RingLocationSharedUsersWriter_SecondWrite_ReplacesUsersDevicesAndPermissions()
        {
            await _writer.ReplaceLocationSharedUsersAsync(_locationId,
                [BuildUser(_locationId, 8001, "a@example.com", true, BuildGrant(1001, "shared", "doorbot", "view", "edit"))],
                CancellationToken.None);
            await _writer.ReplaceLocationSharedUsersAsync(_locationId,
                [BuildUser(_locationId, 8003, "c@example.com", false)],
                CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingLocationSharedUser stored = await read.RingLocationSharedUsers.SingleAsync();
            Assert.Equal(8003L, stored.ProviderUserId);
            Assert.Equal(0, await read.RingLocationSharedUserDevices.CountAsync());
            Assert.Equal(0, await read.RingLocationSharedUserDevicePermissions.CountAsync());
        }

        [Fact]
        public async Task RingLocationSharedUsersWriter_EmptyWrite_ReplacesStoredUsersWithNone()
        {
            await _writer.ReplaceLocationSharedUsersAsync(_locationId, [BuildUser(_locationId, 8001, null, null)], CancellationToken.None);

            await _writer.ReplaceLocationSharedUsersAsync(_locationId, [], CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal(0, await read.RingLocationSharedUsers.CountAsync());
        }

        [Fact]
        public async Task RingLocationSharedUsersWriter_WriteForOneLocation_LeavesAnotherLocationUntouched()
        {
            await _writer.ReplaceLocationSharedUsersAsync(_otherLocationId, [BuildUser(_otherLocationId, 9001, null, null)], CancellationToken.None);

            await _writer.ReplaceLocationSharedUsersAsync(_locationId, [], CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingLocationSharedUser stored = await read.RingLocationSharedUsers.SingleAsync();
            Assert.Equal(_otherLocationId, stored.LocationId);
        }

        [Fact]
        public async Task RingLocationSharedUsersWriter_DuplicateUserInWrite_KeepsPreviousRows()
        {
            await _writer.ReplaceLocationSharedUsersAsync(_locationId,
                [BuildUser(_locationId, 8001, "a@example.com", true, BuildGrant(1001, "shared", "doorbot", "view"))],
                CancellationToken.None);

            // Two rows for one provider user violate the unique (LocationId, ProviderUserId) index, so the insert fails and the transaction rolls back.
            _ = await Assert.ThrowsAnyAsync<DbUpdateException>(() => _writer.ReplaceLocationSharedUsersAsync(_locationId,
                [BuildUser(_locationId, 8005, null, null), BuildUser(_locationId, 8005, null, null)],
                CancellationToken.None));

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingLocationSharedUser stored = await read.RingLocationSharedUsers.SingleAsync();
            Assert.Equal(8001L, stored.ProviderUserId);
            Assert.Equal(1, await read.RingLocationSharedUserDevices.CountAsync());
            Assert.Equal(1, await read.RingLocationSharedUserDevicePermissions.CountAsync());
        }

        [Fact]
        public async Task RingLocationSharedUsersWriter_LocationDeleted_CascadesToDevicesAndPermissions()
        {
            Guid doomedLocationId = Guid.NewGuid();
            await using (VideoForensicsDbContext seed = _fixture.Factory.CreateDbContext())
            {
                _ = seed.Locations.Add(new Location { Id = doomedLocationId, ProviderLocationId = "loc-doomed", Name = "Cabin" });
                _ = await seed.SaveChangesAsync();
            }

            await _writer.ReplaceLocationSharedUsersAsync(doomedLocationId,
                [BuildUser(doomedLocationId, 8001, null, null, BuildGrant(1001, "shared", "doorbot", "view", "edit"))],
                CancellationToken.None);

            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                Location location = await db.Locations.SingleAsync(l => l.Id == doomedLocationId);
                _ = db.Locations.Remove(location);
                _ = await db.SaveChangesAsync();
            }

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal(0, await read.RingLocationSharedUsers.CountAsync());
            Assert.Equal(0, await read.RingLocationSharedUserDevices.CountAsync());
            Assert.Equal(0, await read.RingLocationSharedUserDevicePermissions.CountAsync());
        }

        [Fact]
        public async Task RingChimeLinkedDoorbotsWriter_WriteThenRead_EveryColumnRoundTrips()
        {
            RingChimeLinkedDoorbot doorbot = BuildLink(_chimeId, 0, 1001, "1001", "doorbot", callAlerts: true, motionAlerts: null);
            RingChimeLinkedDoorbot stickup = BuildLink(_chimeId, 1, 2002, null, "stickup_cam", callAlerts: false, motionAlerts: true);
            await _writer.ReplaceChimeLinkedDoorbotsAsync(_chimeId, [doorbot, stickup], CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            List<RingChimeLinkedDoorbot> stored = await read.RingChimeLinkedDoorbots.OrderBy(l => l.Position).ToListAsync();

            Assert.Equal(2, stored.Count);
            RingChimeLinkedDoorbot first = stored[0];
            Assert.Equal(doorbot.Id, first.Id);
            Assert.Equal(_chimeId, first.ChimeDeviceId);
            Assert.Equal(0, first.Position);
            Assert.Equal(1001L, first.LinkedDoorbotId);
            Assert.Equal("1001", first.DeviceId);
            Assert.Equal("doorbot", first.Kind);
            Assert.True(first.CallAlerts);
            Assert.Null(first.MotionAlerts);
            Assert.Equal(Stamp.Ticks, first.UpdatedAtUtc.Ticks);

            RingChimeLinkedDoorbot second = stored[1];
            Assert.Equal(stickup.Id, second.Id);
            Assert.Equal(1, second.Position);
            Assert.Equal(2002L, second.LinkedDoorbotId);
            Assert.Null(second.DeviceId);
            Assert.Equal("stickup_cam", second.Kind);
            Assert.False(second.CallAlerts);
            Assert.True(second.MotionAlerts);
        }

        [Fact]
        public async Task RingChimeLinkedDoorbotsWriter_SecondWrite_ReplacesOnlyThatChimesRows()
        {
            await _writer.ReplaceChimeLinkedDoorbotsAsync(_chimeId, [BuildLink(_chimeId, 0, 1001, "1001", "doorbot", true, true)], CancellationToken.None);
            await _writer.ReplaceChimeLinkedDoorbotsAsync(_otherChimeId, [BuildLink(_otherChimeId, 0, 1002, "1002", "doorbot", true, true)], CancellationToken.None);

            await _writer.ReplaceChimeLinkedDoorbotsAsync(_chimeId, [BuildLink(_chimeId, 0, 2002, "2002", "stickup_cam", false, false)], CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingChimeLinkedDoorbot forFirst = await read.RingChimeLinkedDoorbots.SingleAsync(l => l.ChimeDeviceId == _chimeId);
            Assert.Equal(2002L, forFirst.LinkedDoorbotId);
            RingChimeLinkedDoorbot forOther = await read.RingChimeLinkedDoorbots.SingleAsync(l => l.ChimeDeviceId == _otherChimeId);
            Assert.Equal(1002L, forOther.LinkedDoorbotId);
        }

        [Fact]
        public async Task RingChimeLinkedDoorbotsWriter_EmptyWrite_ReplacesStoredRowsWithNone()
        {
            await _writer.ReplaceChimeLinkedDoorbotsAsync(_chimeId, [BuildLink(_chimeId, 0, 1001, "1001", "doorbot", true, true)], CancellationToken.None);

            await _writer.ReplaceChimeLinkedDoorbotsAsync(_chimeId, [], CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal(0, await read.RingChimeLinkedDoorbots.CountAsync());
        }

        [Fact]
        public async Task RingChimeLinkedDoorbotsWriter_DuplicateLinkInWrite_KeepsPreviousRows()
        {
            await _writer.ReplaceChimeLinkedDoorbotsAsync(_chimeId, [BuildLink(_chimeId, 0, 1001, "1001", "doorbot", true, true)], CancellationToken.None);

            // The unique (ChimeDeviceId, LinkedDoorbotId) index rejects the second 1001, so the previous row must survive.
            _ = await Assert.ThrowsAnyAsync<DbUpdateException>(() => _writer.ReplaceChimeLinkedDoorbotsAsync(_chimeId,
                [BuildLink(_chimeId, 0, 3003, null, null, null, null), BuildLink(_chimeId, 1, 3003, null, null, null, null)],
                CancellationToken.None));

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal(1001L, (await read.RingChimeLinkedDoorbots.SingleAsync()).LinkedDoorbotId);
        }

        [Fact]
        public async Task RingChimeLinkedDoorbotsWriter_ChimeDeleted_CascadesRows()
        {
            await _writer.ReplaceChimeLinkedDoorbotsAsync(_chimeId, [BuildLink(_chimeId, 0, 1001, "1001", "doorbot", true, true)], CancellationToken.None);

            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                Device chime = await db.Devices.SingleAsync(d => d.Id == _chimeId);
                _ = db.Devices.Remove(chime);
                _ = await db.SaveChangesAsync();
            }

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal(0, await read.RingChimeLinkedDoorbots.CountAsync());
        }

        [Fact]
        public async Task RingSnapshotTimestampsWriter_WriteThenRead_EveryColumnRoundTrips()
        {
            await _writer.ReplaceSnapshotTimestampsAsync(_ringAccountId,
                [BuildSnapshot(_ringAccountId, 0, 1001, 1767225600000), BuildSnapshot(_ringAccountId, 1, 2002, null)],
                CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            List<RingSnapshotTimestamp> stored = await read.RingSnapshotTimestamps.OrderBy(t => t.Position).ToListAsync();

            Assert.Equal(2, stored.Count);
            Assert.Equal(_ringAccountId, stored[0].RingAccountId);
            Assert.Equal(0, stored[0].Position);
            Assert.Equal(1001L, stored[0].DoorbotId);
            Assert.Equal(1767225600000L, stored[0].TimestampEpoch);
            Assert.Equal(Stamp.Ticks, stored[0].UpdatedAtUtc.Ticks);
            Assert.NotEqual(Guid.Empty, stored[0].Id);

            Assert.Equal(1, stored[1].Position);
            Assert.Equal(2002L, stored[1].DoorbotId);
            Assert.Null(stored[1].TimestampEpoch);
        }

        [Fact]
        public async Task RingSnapshotTimestampsWriter_SecondWrite_ReplacesAccountRowsOnly()
        {
            Guid otherAccountId = Guid.NewGuid();
            await using (VideoForensicsDbContext seed = _fixture.Factory.CreateDbContext())
            {
                _ = seed.RingAccounts.Add(new RingAccount { Id = otherAccountId, ProviderAccountId = Guid.NewGuid(), SubscriptionLevel = "unknown" });
                _ = await seed.SaveChangesAsync();
            }

            await _writer.ReplaceSnapshotTimestampsAsync(otherAccountId, [BuildSnapshot(otherAccountId, 0, 5005, 1)], CancellationToken.None);
            await _writer.ReplaceSnapshotTimestampsAsync(_ringAccountId, [BuildSnapshot(_ringAccountId, 0, 1001, 1)], CancellationToken.None);

            await _writer.ReplaceSnapshotTimestampsAsync(_ringAccountId, [BuildSnapshot(_ringAccountId, 0, 2002, 2)], CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingSnapshotTimestamp mine = await read.RingSnapshotTimestamps.SingleAsync(t => t.RingAccountId == _ringAccountId);
            Assert.Equal(2002L, mine.DoorbotId);
            RingSnapshotTimestamp theirs = await read.RingSnapshotTimestamps.SingleAsync(t => t.RingAccountId == otherAccountId);
            Assert.Equal(5005L, theirs.DoorbotId);
        }

        [Fact]
        public async Task RingSnapshotTimestampsWriter_EmptyWrite_ReplacesAccountRowsWithNone()
        {
            await _writer.ReplaceSnapshotTimestampsAsync(_ringAccountId, [BuildSnapshot(_ringAccountId, 0, 1001, 1)], CancellationToken.None);

            await _writer.ReplaceSnapshotTimestampsAsync(_ringAccountId, [], CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal(0, await read.RingSnapshotTimestamps.CountAsync());
        }

        [Fact]
        public async Task RingSnapshotTimestampsWriter_RingAccountDeleted_CascadesRows()
        {
            Guid doomedAccountId = Guid.NewGuid();
            await using (VideoForensicsDbContext seed = _fixture.Factory.CreateDbContext())
            {
                _ = seed.RingAccounts.Add(new RingAccount { Id = doomedAccountId, ProviderAccountId = Guid.NewGuid(), SubscriptionLevel = "unknown" });
                _ = await seed.SaveChangesAsync();
            }

            await _writer.ReplaceSnapshotTimestampsAsync(doomedAccountId, [BuildSnapshot(doomedAccountId, 0, 1001, 1)], CancellationToken.None);

            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                RingAccount account = await db.RingAccounts.SingleAsync(a => a.Id == doomedAccountId);
                _ = db.RingAccounts.Remove(account);
                _ = await db.SaveChangesAsync();
            }

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal(0, await read.RingSnapshotTimestamps.CountAsync(t => t.RingAccountId == doomedAccountId));
        }
    }
}
