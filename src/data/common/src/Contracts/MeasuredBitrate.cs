namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// One clip's stream-reported video bitrate, projected from a media item so the bit rate analysis does not load whole rows.
    /// </summary>
    /// <param name="RecordedAtUtc">When the clip was recorded.</param>
    /// <param name="VideoBitrateBps">Video bitrate in bits per second, as the stream reported it (never estimated).</param>
    /// <param name="LightingMode">Lighting class of the clip ("Ir", "ColorNight" or "Day"); null if not analysed.</param>
    /// <param name="RedFlags">Comma-separated red flags raised for the clip; null or empty when none.</param>
    /// <param name="MediaItemId">The clip, so a chart point can link to it.</param>
    public sealed record MeasuredBitrate(DateTime RecordedAtUtc, long VideoBitrateBps, string? LightingMode, string? RedFlags, Guid MediaItemId = default);
}
