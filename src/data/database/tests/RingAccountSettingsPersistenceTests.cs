using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Round-trips the Ring profile, ringtone, light group and location mode graphs through SQLite.</summary>
    public class RingAccountSettingsPersistenceTests : IAsyncLifetime
    {
        private const string LocationId = "loc-1";
        private SqliteInMemoryFixture _fixture = null!;
        private Guid _ringAccountId;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();

            _ringAccountId = Guid.NewGuid();
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.RingAccounts.Add(new RingAccount { Id = _ringAccountId, ProviderAccountId = Guid.NewGuid(), SubscriptionLevel = "unknown" });
            _ = await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private RingAccountProfile BuildProfile(int providerUserId = 42)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                RingAccountId = _ringAccountId,
                ProviderUserId = providerUserId,
                Email = "sanitized.user@example.com",
                CreatedAt = "2021-01-01T00:00:00.000Z",
                TfaEnabled = true,
                SessionFeatures = new RingAccountSessionFeatures { Id = Guid.NewGuid(), RemoteLoggingLevel = 3, VideoSearchEnabled = true },
                Flags =
            [
                new RingAccountProfileFlag
                {
                    Id = Guid.NewGuid(), Scope = "features", Name = "device_types", ValueKind = "list",
                    Items = [new RingAccountProfileFlagItem { Id = Guid.NewGuid(), Position = 0, Value = "doorbell" }]
                }
            ]
            };
        }

        private RingAccountRingtone BuildRingtone(string ringtoneId)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                RingAccountId = _ringAccountId,
                RingtoneId = ringtoneId,
                Kind = "ding",
                Available = true,
                Checksum = 7,
                SupportedDeviceKinds = [new RingAccountRingtoneDeviceKind { Id = Guid.NewGuid(), Position = 0, DeviceKind = "doorbell" }]
            };
        }

        private RingLocationModeSettings BuildModeSettings()
        {
            return new()
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = LocationId,
                LastUpdateTimeMs = 1700000000000,
                DefaultCameraOnlyMode = "away",
                SupportedActionGroups = [new RingLocationModeSupportedActionGroup { Id = Guid.NewGuid(), Position = 0, GroupName = "liveView" }],
                Entries =
            [
                new RingLocationModeSettingsEntry
                {
                    Id = Guid.NewGuid(), Mode = "home", Position = 0, DeviceId = "dev-1", DeviceIdType = "doorbot",
                    Actions =
                    [
                        new RingLocationModeSettingsEntryAction { Id = Guid.NewGuid(), Kind = "enabled", Position = 0, ActionName = "enableMotionDetection" },
                        new RingLocationModeSettingsEntryAction { Id = Guid.NewGuid(), Kind = "supported", ActionGroup = "liveView", Position = 0, ActionName = "allowLiveViewLocally" }
                    ]
                }
            ]
            };
        }

        [Fact]
        public async Task RingAccountSettings_FullGraph_RoundTripsThroughSqlite()
        {
            RingAccountProfile profile = BuildProfile();
            var groupId = Guid.NewGuid();
            var lightGroups = new LightGroupSet
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = LocationId,
                IsOwner = true,
                Groups = [new LightGroup { Id = groupId, ProviderDeviceGroupId = "grp-1", Name = "Backyard", Devices = [new LightGroupDevice { Id = Guid.NewGuid(), ProviderDeviceId = "dev-7" }] }]
            };
            var mode = new RingLocationMode
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = LocationId,
                Mode = "home",
                ReadOnly = false,
                ModePanelType = "panel",
                NotYetParticipating = [new RingLocationModeNotYetParticipating { Id = Guid.NewGuid(), Position = 0, ValueJson = "{\"deviceId\":\"dev-9\"}" }]
            };
            var sharing = new RingLocationModeSharing { Id = Guid.NewGuid(), ProviderLocationId = LocationId, SharedUsersEnabled = false, LastUpdateTimeMs = 5 };
            var defaults = new RingAccountRingtoneDefaults { Id = Guid.NewGuid(), RingAccountId = _ringAccountId, DefaultDingId = "chime_default_ding_2" };

            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                _ = db.RingAccountProfiles.Add(profile);
                _ = db.RingAccountRingtones.Add(BuildRingtone("chime_default_ding_2"));
                _ = db.RingAccountRingtoneDefaults.Add(defaults);
                _ = db.LightGroupSets.Add(lightGroups);
                _ = db.RingLocationModes.Add(mode);
                _ = db.RingLocationModeSettings.Add(BuildModeSettings());
                _ = db.RingLocationModeSharings.Add(sharing);
                _ = await db.SaveChangesAsync();
            }

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingAccountProfile stored = await read.RingAccountProfiles
                .Include(p => p.SessionFeatures)
                .Include(p => p.Flags).ThenInclude(f => f.Items)
                .SingleAsync(p => p.RingAccountId == _ringAccountId);
            Assert.Equal(42, stored.ProviderUserId);
            Assert.Equal(3, stored.SessionFeatures!.RemoteLoggingLevel);
            Assert.Equal("doorbell", Assert.Single(Assert.Single(stored.Flags).Items).Value);

            RingAccountRingtone ringtone = await read.RingAccountRingtones.Include(r => r.SupportedDeviceKinds).SingleAsync();
            Assert.Equal("doorbell", Assert.Single(ringtone.SupportedDeviceKinds).DeviceKind);

            LightGroupSet storedGroups = await read.LightGroupSets.Include(s => s.Groups).ThenInclude(g => g.Devices).SingleAsync();
            Assert.Equal("dev-7", Assert.Single(Assert.Single(storedGroups.Groups).Devices).ProviderDeviceId);

            RingLocationMode storedMode = await read.RingLocationModes.Include(m => m.NotYetParticipating).SingleAsync();
            _ = Assert.Single(storedMode.NotYetParticipating);

            RingLocationModeSettings storedSettings = await read.RingLocationModeSettings
                .Include(s => s.Entries).ThenInclude(e => e.Actions)
                .SingleAsync();
            RingLocationModeSettingsEntry entry = Assert.Single(storedSettings.Entries);
            Assert.Equal(2, entry.Actions.Count);
            Assert.Equal("liveView", entry.Actions.Single(a => a.Kind == "supported").ActionGroup);

            Assert.False((await read.RingLocationModeSharings.SingleAsync()).SharedUsersEnabled);
        }

        [Fact]
        public async Task RingAccountProfile_SecondRowForSameRingAccount_IsRejected()
        {
            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                _ = db.RingAccountProfiles.Add(BuildProfile());
                _ = await db.SaveChangesAsync();
            }

            await using VideoForensicsDbContext second = _fixture.Factory.CreateDbContext();
            _ = second.RingAccountProfiles.Add(BuildProfile(providerUserId: 43));
            _ = await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.SaveChangesAsync());
        }

        [Fact]
        public async Task RingLocationMode_SecondRowForSameLocation_IsRejected()
        {
            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                _ = db.RingLocationModes.Add(new RingLocationMode { Id = Guid.NewGuid(), ProviderLocationId = LocationId, Mode = "away" });
                _ = await db.SaveChangesAsync();
            }

            await using VideoForensicsDbContext second = _fixture.Factory.CreateDbContext();
            _ = second.RingLocationModes.Add(new RingLocationMode { Id = Guid.NewGuid(), ProviderLocationId = LocationId, Mode = "home" });
            _ = await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.SaveChangesAsync());
        }
    }
}
