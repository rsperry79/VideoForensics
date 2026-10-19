namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Request body for turning a device floodlight on or off.</summary>
    /// <param name="On">True to turn the light on, false to turn it off.</param>
    public record SetLightRequest(bool On);

    /// <summary>Request body for turning a device siren on or off.</summary>
    /// <param name="On">True to sound the siren, false to stop it.</param>
    /// <param name="DurationSeconds">Seconds to sound the siren for (1 to 3600). Null lets the provider default apply.</param>
    public record SetSirenRequest(bool On, int? DurationSeconds);

    /// <summary>Request body for setting a device volume.</summary>
    /// <param name="Volume">Volume level, 0 to 11.</param>
    public record SetVolumeRequest(int Volume);

    /// <summary>Request body for enabling or disabling night mode on a device.</summary>
    /// <param name="Enabled">True to enable night mode.</param>
    public record SetNightModeRequest(bool Enabled);

    /// <summary>Request body for enabling or disabling motion detection on a device.</summary>
    /// <param name="Enabled">True to enable motion detection.</param>
    public record SetMotionDetectionRequest(bool Enabled);

    /// <summary>Request body for silencing a chime notifications.</summary>
    /// <param name="Seconds">Seconds to silence the chime for (1 to 86400).</param>
    public record SetDoNotDisturbRequest(int Seconds);
}
