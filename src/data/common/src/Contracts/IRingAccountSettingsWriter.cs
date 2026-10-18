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
    }
}
