using Microsoft.Extensions.Logging;

using Moq;

using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Feeds the sanitized captures through the persister into a mocked writer, and checks the mapped graph that
    /// reaches the writer. Expected values come from the fixtures. A failing write must only log an error.
    /// </summary>
    public class RingAccountSettingsPersisterTests
    {
        private const string RedactedToken = "REDACTED-NOT-STORED";
        private const string LocationId = "dc5ff64d-bb2e-4c1f-9b74-ee0db6a8d2e9";
        private static readonly Guid AccountId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static string ReadFixture(string name)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        }

        [Fact]
        public async Task RingAccountSettingsPersister_PersistProfileAsync_CapturedProfile_WritesProfileWithoutToken()
        {
            Profile profile = JsonSerializer.Deserialize<ProfileResponse>(ReadFixture("profile-sanitized.json"))!.Profile;
            var writer = new Mock<IRingAccountSettingsWriter>();
            RingAccountProfile? written = null;
            _ = writer.Setup(w => w.ReplaceProfileAsync(It.IsAny<RingAccountProfile>(), It.IsAny<CancellationToken>()))
                .Callback<RingAccountProfile, CancellationToken>((p, _) => written = p)
                .Returns(Task.CompletedTask);

            await CreatePersister(writer).PersistProfileAsync(profile, AccountId);

            Assert.NotNull(written);
            Assert.Equal(AccountId, written!.RingAccountId);
            Assert.Equal(profile.Id, written.ProviderUserId);
            Assert.DoesNotContain(written.Flags, f => f.Name.Contains("authentication_token", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(written.Flags, f => f.StringValue == RedactedToken);
            Assert.DoesNotContain(written.Flags.SelectMany(f => f.Items), i => i.Value == RedactedToken);
        }

        [Fact]
        public async Task RingAccountSettingsPersister_PersistRingtonesAsync_CapturedRingtones_WritesRingtonesAndDefaults()
        {
            RingtonesResponse response = JsonSerializer.Deserialize<RingtonesResponse>(ReadFixture("ringtones-sanitized.json"))!;
            var writer = new Mock<IRingAccountSettingsWriter>();
            IReadOnlyList<RingAccountRingtone>? ringtones = null;
            RingAccountRingtoneDefaults? defaults = null;
            _ = writer.Setup(w => w.ReplaceRingtonesAsync(AccountId, It.IsAny<IReadOnlyList<RingAccountRingtone>>(), It.IsAny<RingAccountRingtoneDefaults>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, IReadOnlyList<RingAccountRingtone>, RingAccountRingtoneDefaults, CancellationToken>((_, r, d, _) => { ringtones = r; defaults = d; })
                .Returns(Task.CompletedTask);

            await CreatePersister(writer).PersistRingtonesAsync(response, AccountId);

            Assert.NotNull(ringtones);
            Assert.Equal(response.Ringtones!.Count, ringtones!.Count);
            Assert.Equal(response.DefaultDingId, defaults!.DefaultDingId);
            Assert.Equal(AccountId, defaults.RingAccountId);
        }

        [Fact]
        public async Task RingAccountSettingsPersister_PersistLightGroupsAsync_CapturedGroups_WritesSetForLocation()
        {
            GroupsResponse response = JsonSerializer.Deserialize<GroupsResponse>(ReadFixture("light-groups-sanitized.json"))!;
            var writer = new Mock<IRingAccountSettingsWriter>();
            LightGroupSet? written = null;
            _ = writer.Setup(w => w.ReplaceLightGroupSetAsync(It.IsAny<LightGroupSet>(), It.IsAny<CancellationToken>()))
                .Callback<LightGroupSet, CancellationToken>((s, _) => written = s)
                .Returns(Task.CompletedTask);

            await CreatePersister(writer).PersistLightGroupsAsync(response, LocationId);

            Assert.NotNull(written);
            Assert.Equal(LocationId, written!.ProviderLocationId);
            Assert.Equal(response.IsOwner, written.IsOwner);
            Assert.Equal(response.DeviceGroups?.Count ?? 0, written.Groups.Count);
        }

        [Fact]
        public async Task RingAccountSettingsPersister_PersistLocationModeAsync_CapturedMode_WritesModeForLocation()
        {
            LocationMode mode = JsonSerializer.Deserialize<LocationMode>(ReadFixture("location-mode-sanitized.json"))!;
            var writer = new Mock<IRingAccountSettingsWriter>();
            RingLocationMode? written = null;
            _ = writer.Setup(w => w.ReplaceLocationModeAsync(It.IsAny<RingLocationMode>(), It.IsAny<CancellationToken>()))
                .Callback<RingLocationMode, CancellationToken>((m, _) => written = m)
                .Returns(Task.CompletedTask);

            await CreatePersister(writer).PersistLocationModeAsync(mode, LocationId);

            Assert.NotNull(written);
            Assert.Equal(LocationId, written!.ProviderLocationId);
            Assert.Equal(mode.Mode, written.Mode);
            Assert.Equal(mode.ModePanelType, written.ModePanelType);
        }

        [Fact]
        public async Task RingAccountSettingsPersister_PersistLocationModeSettingsAsync_CapturedSettings_WritesSettingsForLocation()
        {
            LocationModeSettingsResponse response = JsonSerializer.Deserialize<LocationModeSettingsResponse>(ReadFixture("location-mode-settings-sanitized.json"))!;
            var writer = new Mock<IRingAccountSettingsWriter>();
            RingLocationModeSettings? written = null;
            _ = writer.Setup(w => w.ReplaceLocationModeSettingsAsync(It.IsAny<RingLocationModeSettings>(), It.IsAny<CancellationToken>()))
                .Callback<RingLocationModeSettings, CancellationToken>((s, _) => written = s)
                .Returns(Task.CompletedTask);

            await CreatePersister(writer).PersistLocationModeSettingsAsync(response, LocationId);

            Assert.NotNull(written);
            Assert.Equal(LocationId, written!.ProviderLocationId);
            Assert.Equal(response.Options?.DefaultCameraOnlyMode, written.DefaultCameraOnlyMode);
            Assert.NotEmpty(written.Entries);
        }

        [Fact]
        public async Task RingAccountSettingsPersister_PersistLocationModeSharingAsync_CapturedSharing_WritesSharingForLocation()
        {
            LocationModeSharingResponse response = JsonSerializer.Deserialize<LocationModeSharingResponse>(ReadFixture("location-mode-sharing-sanitized.json"))!;
            var writer = new Mock<IRingAccountSettingsWriter>();
            RingLocationModeSharing? written = null;
            _ = writer.Setup(w => w.ReplaceLocationModeSharingAsync(It.IsAny<RingLocationModeSharing>(), It.IsAny<CancellationToken>()))
                .Callback<RingLocationModeSharing, CancellationToken>((s, _) => written = s)
                .Returns(Task.CompletedTask);

            await CreatePersister(writer).PersistLocationModeSharingAsync(response, LocationId);

            Assert.NotNull(written);
            Assert.Equal(LocationId, written!.ProviderLocationId);
            Assert.Equal(response.SharedUsersEnabled, written.SharedUsersEnabled);
        }

        [Fact]
        public async Task RingAccountSettingsPersister_PersistLocationModeAsync_WriterThrows_LogsErrorAndDoesNotThrow()
        {
            LocationMode mode = JsonSerializer.Deserialize<LocationMode>(ReadFixture("location-mode-sanitized.json"))!;
            var writer = new Mock<IRingAccountSettingsWriter>();
            _ = writer.Setup(w => w.ReplaceLocationModeAsync(It.IsAny<RingLocationMode>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("disk full"));
            var logger = new RecordingLogger();

            await new RingAccountSettingsPersister(writer.Object, logger).PersistLocationModeAsync(mode, LocationId);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
        }

        private static RingAccountSettingsPersister CreatePersister(Mock<IRingAccountSettingsWriter> writer)
        {
            return new(writer.Object, new RecordingLogger());
        }

        /// <summary>Keeps each log entry's level so a test can assert an error was written.</summary>
        private sealed class RecordingLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
