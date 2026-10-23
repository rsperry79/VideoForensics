namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Provider-agnostic device controls (floodlight, siren, volume, night mode, motion detection, do-not-disturb).
    /// Every method takes the provider's own device id. Implementations throw on failure with a message that
    /// does not contain provider URLs or ids beyond the device id. Input range validation happens at the API
    /// boundary, not here.
    /// </summary>
    public interface IDeviceControlService
    {
        /// <summary>Turns the device's floodlight on or off.</summary>
        Task SetLightAsync(string providerDeviceId, bool on, CancellationToken ct);

        /// <summary>Turns the device's siren on or off. <paramref name="durationSeconds"/> applies only when turning on; null lets the provider default apply.</summary>
        Task SetSirenAsync(string providerDeviceId, bool on, int? durationSeconds, CancellationToken ct);

        /// <summary>Sets the device's ringtone/notification volume.</summary>
        Task SetVolumeAsync(string providerDeviceId, int volume, CancellationToken ct);

        /// <summary>Enables or disables the device's night mode.</summary>
        Task SetNightModeAsync(string providerDeviceId, bool enabled, CancellationToken ct);

        /// <summary>Enables or disables the device's motion detection.</summary>
        Task SetMotionDetectionAsync(string providerDeviceId, bool enabled, CancellationToken ct);

        /// <summary>Silences the chime for the given number of seconds. Applies to chimes only.</summary>
        Task SetDoNotDisturbAsync(string providerDeviceId, int seconds, CancellationToken ct);
    }
}
