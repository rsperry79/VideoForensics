using Microsoft.Extensions.Logging;

using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
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
        public Task PersistProfileAsync(Profile profile, Guid ringAccountId, CancellationToken ct = default)
        {
            return TryWriteAsync("profile", () => _writer.ReplaceProfileAsync(RingAccountSettingsMapper.MapProfile(profile, ringAccountId), ct), ct);
        }

        /// <summary>Persists the ringtone list and the default ringtone row.</summary>
        public Task PersistRingtonesAsync(RingtonesResponse response, Guid ringAccountId, CancellationToken ct = default)
        {
            return TryWriteAsync("ringtone", () => _writer.ReplaceRingtonesAsync(
                ringAccountId,
                RingAccountSettingsMapper.MapRingtones(response, ringAccountId),
                RingAccountSettingsMapper.MapRingtoneDefaults(response, ringAccountId),
                ct), ct);
        }

        /// <summary>Persists the Smart Lighting group set for a location.</summary>
        public Task PersistLightGroupsAsync(GroupsResponse response, string providerLocationId, CancellationToken ct = default)
        {
            return TryWriteAsync("light group", () => _writer.ReplaceLightGroupSetAsync(
                RingAccountSettingsMapper.MapLightGroups(response, providerLocationId), ct), ct);
        }

        /// <summary>Persists the current location mode for a location.</summary>
        public Task PersistLocationModeAsync(LocationMode mode, string providerLocationId, CancellationToken ct = default)
        {
            return TryWriteAsync("location mode", () => _writer.ReplaceLocationModeAsync(
                RingAccountSettingsMapper.MapLocationMode(mode, providerLocationId), ct), ct);
        }

        /// <summary>Persists the location mode settings for a location.</summary>
        public Task PersistLocationModeSettingsAsync(LocationModeSettingsResponse response, string providerLocationId, CancellationToken ct = default)
        {
            return TryWriteAsync("location mode settings", () => _writer.ReplaceLocationModeSettingsAsync(
                RingAccountSettingsMapper.MapLocationModeSettings(response, providerLocationId), ct), ct);
        }

        /// <summary>Persists the location mode sharing state for a location.</summary>
        public Task PersistLocationModeSharingAsync(LocationModeSharingResponse response, string providerLocationId, CancellationToken ct = default)
        {
            return TryWriteAsync("location mode sharing", () => _writer.ReplaceLocationModeSharingAsync(
                RingAccountSettingsMapper.MapLocationModeSharing(response, providerLocationId), ct), ct);
        }

        /// <summary>Persists the alarm account monitoring status for a location.</summary>
        /// <summary>Replaces the Ring-only location details (devices/v1/locations) for one location.</summary>
        public Task PersistLocationDetailsAsync(RingLocationDetails details, CancellationToken ct = default)
        {
            return TryWriteAsync("location details", () => _writer.ReplaceLocationDetailsAsync(details, ct), ct);
        }

        public Task PersistAccountMonitoringStatusAsync(AccountMonitoringStatusResponse response, string providerLocationId, CancellationToken ct = default)
        {
            return TryWriteAsync("account monitoring status", () => _writer.ReplaceAccountMonitoringStatusAsync(
                RingAccountSettingsMapper.MapAccountMonitoringStatus(response, providerLocationId), ct), ct);
        }

        /// <summary>
        /// Replaces the shared users of one location. <paramref name="locationId"/> is the stored Location row's id (the rows
        /// reference Locations.Id), not the Ring location id. Returns true when the rows were stored.
        /// </summary>
        public Task<bool> PersistLocationSharedUsersAsync(IReadOnlyList<SharedUser> users, Guid locationId, CancellationToken ct = default)
        {
            return TryWriteAsync("shared users", () => _writer.ReplaceLocationSharedUsersAsync(
                locationId, RingSmallEndpointMapper.MapLocationSharedUsers(users, locationId), ct), ct);
        }

        /// <summary>
        /// Replaces the doorbots linked to one stored chime. <paramref name="chimeDeviceId"/> is the stored Devices row's id.
        /// A body with no linked_doorbots list is a null result: nothing is written and false is returned, so the stored rows stay.
        /// The caller logs that case with the device name. Returns true when the rows were stored.
        /// </summary>
        public async Task<bool> PersistChimeLinkedDoorbotsAsync(JsonElement response, Guid chimeDeviceId, CancellationToken ct = default)
        {
            IReadOnlyList<RingChimeLinkedDoorbot>? links = RingSmallEndpointMapper.MapChimeLinkedDoorbots(response, chimeDeviceId);
            if (links is null)
            {
                return false;
            }

            return await TryWriteAsync("linked chime doorbots", () => _writer.ReplaceChimeLinkedDoorbotsAsync(chimeDeviceId, links, ct), ct);
        }

        /// <summary>
        /// Replaces the snapshot timestamps of one Ring account with the timestamps in <paramref name="response"/>. The caller
        /// passes every doorbot's answer combined, because the rows are replaced as one set per account. Returns true when stored.
        /// </summary>
        public Task<bool> PersistSnapshotTimestampsAsync(DoorbotTimestamps response, Guid ringAccountId, CancellationToken ct = default)
        {
            return TryWriteAsync("snapshot timestamps", () => _writer.ReplaceSnapshotTimestampsAsync(
                ringAccountId, RingSmallEndpointMapper.MapSnapshotTimestamps(response, ringAccountId), ct), ct);
        }

        // Mapping runs inside the guarded call, so a malformed response is logged the same way as a failed write.
        // Returns true when the write completed and false when it failed (the failure is logged).
        private async Task<bool> TryWriteAsync(string area, Func<Task> write, CancellationToken ct)
        {
            try
            {
                await write();
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Ring {Area} settings were not stored; sync continues: {Message} ({ErrorType})", area, ex.Message, ex.GetType().Name);
                return false;
            }
        }
    }
}
