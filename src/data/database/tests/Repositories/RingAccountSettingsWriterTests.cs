using Microsoft.EntityFrameworkCore;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Each writer replaces its parent's graph in one transaction: the second write leaves only the new children,
    /// and a failed insert leaves the previous graph untouched.
    /// </summary>
    public class RingAccountSettingsWriterTests : IAsyncLifetime
    {
        private const string LocationId = "loc-1";
        private SqliteInMemoryFixture _fixture = null!;
        private RingAccountSettingsWriter _writer = null!;
        private Guid _ringAccountId;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _writer = new RingAccountSettingsWriter(_fixture.Factory);

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

        private RingAccountProfile BuildProfile(string flagName, string email = "sanitized.user@example.com")
        {
            return new()
            {
                Id = Guid.NewGuid(),
                RingAccountId = _ringAccountId,
                ProviderUserId = 42,
                Email = email,
                SessionFeatures = new RingAccountSessionFeatures { Id = Guid.NewGuid(), RemoteLoggingLevel = 3 },
                Flags =
            [
                new RingAccountProfileFlag
                {
                    Id = Guid.NewGuid(), Scope = "features", Name = flagName, ValueKind = "list",
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
                SupportedDeviceKinds = [new RingAccountRingtoneDeviceKind { Id = Guid.NewGuid(), Position = 0, DeviceKind = "doorbell" }]
            };
        }

        private RingAccountRingtoneDefaults BuildDefaults(string defaultDingId)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                RingAccountId = _ringAccountId,
                DefaultDingId = defaultDingId,
            };
        }

        private static LightGroupSet BuildLightGroups(string providerLocationId, string groupId, string? deviceId)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                IsOwner = true,
                Groups =
            [
                new LightGroup
                {
                    Id = Guid.NewGuid(), ProviderDeviceGroupId = groupId, Name = groupId,
                    Devices = deviceId is null ? [] : [new LightGroupDevice { Id = Guid.NewGuid(), ProviderDeviceId = deviceId }]
                }
            ]
            };
        }

        private static RingLocationMode BuildMode(string mode, int notYetParticipating)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = LocationId,
                Mode = mode,
                NotYetParticipating = Enumerable.Range(0, notYetParticipating)
                .Select(i => new RingLocationModeNotYetParticipating { Id = Guid.NewGuid(), Position = i, ValueJson = "{}" })
                .ToList()
            };
        }

        private static RingLocationModeSettings BuildSettings(string actionName, int actionCount)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = LocationId,
                DefaultCameraOnlyMode = "away",
                Entries =
            [
                new RingLocationModeSettingsEntry
                {
                    Id = Guid.NewGuid(), Mode = "home", Position = 0, DeviceId = "dev-1", DeviceIdType = "doorbot",
                    Actions = Enumerable.Range(0, actionCount)
                        .Select(i => new RingLocationModeSettingsEntryAction { Id = Guid.NewGuid(), Kind = "enabled", Position = i, ActionName = actionName })
                        .ToList()
                }
            ]
            };
        }

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceProfileAsync_SecondWrite_ReplacesFlagsAndFeatures()
        {
            await _writer.ReplaceProfileAsync(BuildProfile("device_types"), CancellationToken.None);
            await _writer.ReplaceProfileAsync(BuildProfile("new_flag", email: "second@example.com"), CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingAccountProfile stored = await read.RingAccountProfiles.Include(p => p.Flags).ThenInclude(f => f.Items).SingleAsync();
            Assert.Equal("second@example.com", stored.Email);
            Assert.Equal("new_flag", Assert.Single(stored.Flags).Name);
            Assert.Equal(1, await read.RingAccountProfileFlags.CountAsync());
            Assert.Equal(1, await read.RingAccountProfileFlagItems.CountAsync());
            Assert.Equal(1, await read.RingAccountSessionFeatures.CountAsync());
        }

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceRingtonesAsync_SecondWrite_ReplacesRingtonesAndDefaults()
        {
            await _writer.ReplaceRingtonesAsync(
                _ringAccountId, [BuildRingtone("chime_a"), BuildRingtone("chime_b")], BuildDefaults("chime_a"), CancellationToken.None);
            await _writer.ReplaceRingtonesAsync(
                _ringAccountId, [BuildRingtone("chime_c")], BuildDefaults("chime_c"), CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingAccountRingtone ringtone = await read.RingAccountRingtones.SingleAsync();
            Assert.Equal("chime_c", ringtone.RingtoneId);
            Assert.Equal(1, await read.RingAccountRingtoneDeviceKinds.CountAsync());
            Assert.Equal("chime_c", (await read.RingAccountRingtoneDefaults.SingleAsync()).DefaultDingId);
        }

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceRingtonesAsync_DuplicateRingtoneId_KeepsPreviousRows()
        {
            await _writer.ReplaceRingtonesAsync(_ringAccountId, [BuildRingtone("chime_a")], BuildDefaults("chime_a"), CancellationToken.None);

            // Two rows with the same ringtone id violate the unique index, so the insert fails after the delete.
            _ = await Assert.ThrowsAnyAsync<DbUpdateException>(() => _writer.ReplaceRingtonesAsync(
                _ringAccountId, [BuildRingtone("chime_x"), BuildRingtone("chime_x")], BuildDefaults("chime_x"), CancellationToken.None));

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal("chime_a", (await read.RingAccountRingtones.SingleAsync()).RingtoneId);
            Assert.Equal("chime_a", (await read.RingAccountRingtoneDefaults.SingleAsync()).DefaultDingId);
        }

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceLightGroupSetAsync_SecondWrite_ReplacesGroupsAndDevices()
        {
            await _writer.ReplaceLightGroupSetAsync(BuildLightGroups(LocationId, "grp-1", "dev-7"), CancellationToken.None);
            await _writer.ReplaceLightGroupSetAsync(BuildLightGroups(LocationId, "grp-2", null), CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            LightGroupSet stored = await read.LightGroupSets.Include(s => s.Groups).SingleAsync();
            Assert.Equal("grp-2", Assert.Single(stored.Groups).ProviderDeviceGroupId);
            Assert.Equal(0, await read.LightGroupDevices.CountAsync());
        }

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceLocationModeAsync_SecondWrite_ReplacesRowAndChildren()
        {
            await _writer.ReplaceLocationModeAsync(BuildMode("home", notYetParticipating: 2), CancellationToken.None);
            await _writer.ReplaceLocationModeAsync(BuildMode("away", notYetParticipating: 0), CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingLocationMode stored = await read.RingLocationModes.SingleAsync();
            Assert.Equal("away", stored.Mode);
            Assert.Equal(0, await read.RingLocationModeNotYetParticipating.CountAsync());
        }

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceLocationModeSettingsAsync_SecondWrite_ReplacesEntriesAndActions()
        {
            await _writer.ReplaceLocationModeSettingsAsync(BuildSettings("enableMotionDetection", actionCount: 2), CancellationToken.None);
            await _writer.ReplaceLocationModeSettingsAsync(BuildSettings("allowLiveViewLocally", actionCount: 1), CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingLocationModeSettings stored = await read.RingLocationModeSettings.Include(s => s.Entries).ThenInclude(e => e.Actions).SingleAsync();
            RingLocationModeSettingsEntry entry = Assert.Single(stored.Entries);
            Assert.Equal("allowLiveViewLocally", Assert.Single(entry.Actions).ActionName);
            Assert.Equal(1, await read.RingLocationModeSettingsEntries.CountAsync());
            Assert.Equal(1, await read.RingLocationModeSettingsEntryActions.CountAsync());
        }

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceLocationModeSharingAsync_SecondWrite_ReplacesRow()
        {
            await _writer.ReplaceLocationModeSharingAsync(
                new RingLocationModeSharing { Id = Guid.NewGuid(), ProviderLocationId = LocationId, SharedUsersEnabled = true, LastUpdateTimeMs = 1 },
                CancellationToken.None);
            await _writer.ReplaceLocationModeSharingAsync(
                new RingLocationModeSharing { Id = Guid.NewGuid(), ProviderLocationId = LocationId, SharedUsersEnabled = false, LastUpdateTimeMs = 2 },
                CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingLocationModeSharing stored = await read.RingLocationModeSharings.SingleAsync();
            Assert.False(stored.SharedUsersEnabled);
            Assert.Equal(2, stored.LastUpdateTimeMs);
        }
    

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceAccountMonitoringStatusAsync_StoresEveryFieldAndPlans()
        {
            RingAccountMonitoringStatus status = NewMonitoringStatus("ring_protect_plus", "ring_protect_pro");

            await _writer.ReplaceAccountMonitoringStatusAsync(status, CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            RingAccountMonitoringStatus stored = await read.RingAccountMonitoringStatuses.Include(s => s.CompatibleMonitoringPlans).SingleAsync();
            Assert.Equal(LocationId, stored.ProviderLocationId);
            Assert.True(stored.DispatchSetupComplete);
            Assert.False(stored.AddressComplete);
            Assert.Equal("full", stored.CmsMonitoringType);
            Assert.Equal("HARDWARE", stored.AccountStateReason);
            Assert.Equal("UNAVAILABLE", stored.AccountState);
            Assert.Equal("rrms", stored.ExternalServiceConfigType);
            Assert.Equal("stripe", stored.PlanProvider);
            Assert.Equal(1787519584857, stored.InstallationDate);
            Assert.Equal(new[] { "ring_protect_plus", "ring_protect_pro" }, stored.CompatibleMonitoringPlans.OrderBy(p => p.Position).Select(p => p.Plan));
        }

        [Fact]
        public async Task RingAccountSettingsWriter_ReplaceAccountMonitoringStatusAsync_SecondWrite_ReplacesRowAndPlans()
        {
            await _writer.ReplaceAccountMonitoringStatusAsync(NewMonitoringStatus("ring_protect_plus", "ring_protect_pro"), CancellationToken.None);
            await _writer.ReplaceAccountMonitoringStatusAsync(NewMonitoringStatus("ring_protect_basic"), CancellationToken.None);

            await using VideoForensicsDbContext read = _fixture.Factory.CreateDbContext();
            Assert.Equal(1, await read.RingAccountMonitoringStatuses.CountAsync());
            RingAccountMonitoringPlan plan = await read.RingAccountMonitoringPlans.SingleAsync();
            Assert.Equal("ring_protect_basic", plan.Plan);
        }

        private static RingAccountMonitoringStatus NewMonitoringStatus(params string[] plans)
        {
            var status = new RingAccountMonitoringStatus
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = LocationId,
                AddressComplete = false,
                DispatchSetupComplete = true,
                CmsMonitoringType = "full",
                AccountStateReason = "HARDWARE",
                AccountUuid = Guid.NewGuid().ToString(),
                AccountState = "UNAVAILABLE",
                ExternalServiceConfigType = "rrms",
                PlanProvider = "stripe",
                InstallationDate = 1787519584857,
                SosOnlyFlagOrFalse = false,
            };
            for (int i = 0; i < plans.Length; i++)
            {
                status.CompatibleMonitoringPlans.Add(new RingAccountMonitoringPlan { Id = Guid.NewGuid(), RingAccountMonitoringStatusId = status.Id, Position = i, Plan = plans[i] });
            }

            return status;
        }
    }
}
