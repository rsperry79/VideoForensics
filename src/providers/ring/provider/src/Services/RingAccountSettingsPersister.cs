using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Maps each Ring settings response and hands the graph to <see cref="IRingAccountSettingsWriter"/>.
    /// A failed write is logged as a warning and not rethrown, so the rest of a sync continues.
    /// Ring account ids are never logged: the log names the settings area only.
    /// </summary>
    public class RingAccountSettingsPersister
    {
        private readonly IRingAccountSettingsWriter _writer;
        private readonly ILogger _logger;

        public RingAccountSettingsPersister(IRingAccountSettingsWriter writer, ILogger logger)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Persists the profile. The authentication token is excluded by the mapper and never reaches the writer.</summary>
        public Task PersistProfileAsync(Profile profile, Guid ringAccountId, CancellationToken ct = default) =>
            TryWriteAsync("profile", () => _writer.ReplaceProfileAsync(RingAccountSettingsMapper.MapProfile(profile, ringAccountId), ct), ct);

        /// <summary>Persists the ringtone list and the default ringtone row.</summary>
        public Task PersistRingtonesAsync(RingtonesResponse response, Guid ringAccountId, CancellationToken ct = default) =>
            TryWriteAsync("ringtone", () => _writer.ReplaceRingtonesAsync(
                ringAccountId,
                RingAccountSettingsMapper.MapRingtones(response, ringAccountId),
                RingAccountSettingsMapper.MapRingtoneDefaults(response, ringAccountId),
                ct), ct);

        /// <summary>Persists the Smart Lighting group set for a location.</summary>
        public Task PersistLightGroupsAsync(GroupsResponse response, string providerLocationId, CancellationToken ct = default) =>
            TryWriteAsync("light group", () => _writer.ReplaceLightGroupSetAsync(
                RingAccountSettingsMapper.MapLightGroups(response, providerLocationId), ct), ct);

        /// <summary>Persists the current location mode for a location.</summary>
        public Task PersistLocationModeAsync(LocationMode mode, string providerLocationId, CancellationToken ct = default) =>
            TryWriteAsync("location mode", () => _writer.ReplaceLocationModeAsync(
                RingAccountSettingsMapper.MapLocationMode(mode, providerLocationId), ct), ct);

        /// <summary>Persists the location mode settings for a location.</summary>
        public Task PersistLocationModeSettingsAsync(LocationModeSettingsResponse response, string providerLocationId, CancellationToken ct = default) =>
            TryWriteAsync("location mode settings", () => _writer.ReplaceLocationModeSettingsAsync(
                RingAccountSettingsMapper.MapLocationModeSettings(response, providerLocationId), ct), ct);

        /// <summary>Persists the location mode sharing state for a location.</summary>
        public Task PersistLocationModeSharingAsync(LocationModeSharingResponse response, string providerLocationId, CancellationToken ct = default) =>
            TryWriteAsync("location mode sharing", () => _writer.ReplaceLocationModeSharingAsync(
                RingAccountSettingsMapper.MapLocationModeSharing(response, providerLocationId), ct), ct);

        // Mapping runs inside the guarded call, so a malformed response is logged the same way as a failed write.
        private async Task TryWriteAsync(string area, Func<Task> write, CancellationToken ct)
        {
            try
            {
                await write();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ring {Area} settings were not stored; sync continues", area);
            }
        }
    }
}
