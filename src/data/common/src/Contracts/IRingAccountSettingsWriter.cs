using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Persists the Ring profile, ringtone, Smart Lighting group and location mode graphs.
    /// Each method replaces the existing graph for its parent key (account or location) in one transaction:
    /// the parent's child rows are deleted, then the new graph is inserted. Children are never merged.
    /// </summary>
    public interface IRingAccountSettingsWriter
    {
        /// <summary>Replaces the profile for the Ring account the profile belongs to (keyed by <see cref="RingAccountProfile.RingAccountId"/>).</summary>
        Task ReplaceProfileAsync(RingAccountProfile profile, CancellationToken ct);

        /// <summary>Replaces every ringtone and the default ringtone row for a Ring account.</summary>
        Task ReplaceRingtonesAsync(Guid ringAccountId, IReadOnlyList<RingAccountRingtone> ringtones, RingAccountRingtoneDefaults defaults, CancellationToken ct);

        /// <summary>Replaces the Smart Lighting group set for a location (keyed by <see cref="LightGroupSet.ProviderLocationId"/>).</summary>
        Task ReplaceLightGroupSetAsync(LightGroupSet set, CancellationToken ct);

        /// <summary>Replaces the current location mode for a location (keyed by <see cref="RingLocationMode.ProviderLocationId"/>).</summary>
        Task ReplaceLocationModeAsync(RingLocationMode mode, CancellationToken ct);

        /// <summary>Replaces the location mode settings for a location (keyed by <see cref="RingLocationModeSettings.ProviderLocationId"/>).</summary>
        Task ReplaceLocationModeSettingsAsync(RingLocationModeSettings settings, CancellationToken ct);

        /// <summary>Replaces the location mode sharing state for a location (keyed by <see cref="RingLocationModeSharing.ProviderLocationId"/>).</summary>
        Task ReplaceLocationModeSharingAsync(RingLocationModeSharing sharing, CancellationToken ct);

        /// <summary>Replaces the alarm account monitoring status and its plans for a location (keyed by <see cref="RingAccountMonitoringStatus.ProviderLocationId"/>).</summary>
        Task ReplaceAccountMonitoringStatusAsync(RingAccountMonitoringStatus status, CancellationToken ct);

        /// <summary>Replaces the Ring-only details of a location (keyed by <see cref="RingLocationDetails.LocationId"/>).</summary>
        Task ReplaceLocationDetailsAsync(RingLocationDetails details, CancellationToken ct);

        /// <summary>Replaces the users shared onto a location, with their device grants and permissions.</summary>
        Task ReplaceLocationSharedUsersAsync(Guid locationId, IReadOnlyList<RingLocationSharedUser> users, CancellationToken ct);

        /// <summary>Replaces the doorbots linked to one chime.</summary>
        Task ReplaceChimeLinkedDoorbotsAsync(Guid chimeDeviceId, IReadOnlyList<RingChimeLinkedDoorbot> links, CancellationToken ct);

        /// <summary>Replaces the snapshot timestamps of one Ring account.</summary>
        Task ReplaceSnapshotTimestampsAsync(Guid ringAccountId, IReadOnlyList<RingSnapshotTimestamp> timestamps, CancellationToken ct);
    }
}
