namespace VideoForensics.Data.Common.Entities
{
    /// <summary>The kind of anomaly a <see cref="MediaFlag"/> marks for review. Stored by name, so append new values and never rename.</summary>
    public enum MediaFlagType
    {
        BitrateCollapse,
        ClippedAudio,
        ElevatedNoiseFloor,
        UnusualLighting,
        AudioDropout,
        RssiDeviation,
        Jamming,
        BitrateDeviation
    }
}
