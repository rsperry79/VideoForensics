namespace VideoForensics.Core.Providers.Core
{
    /// <summary>One independent sign that a clip may show interference. A clip can carry several.</summary>
    public enum RedFlag
    {
        /// <summary>Video bitrate fell well below the same-mode baseline for the device.</summary>
        BitrateCollapse,

        /// <summary>The audio has many clipped peaks, which a nearby overload or source produces.</summary>
        ClippedAudio,

        /// <summary>The background noise floor rose well above the device's own audio baseline.</summary>
        ElevatedNoiseFloor,

        /// <summary>The lighting mode is unusual for this device.</summary>
        UnusualLighting
    }

    /// <summary>The measurements a clip's red flags are judged from. Null means the measurement is unavailable.</summary>
    /// <param name="BitrateCollapsed">True when <see cref="InterferenceAssessment.InterferenceSuspected"/> was set.</param>
    /// <param name="ClippedPeakCount">Number of clipped audio peaks in the clip.</param>
    /// <param name="NoiseFloorRiseDb">Noise floor above the device's audio baseline, in dB; null if no baseline.</param>
    /// <param name="UnusualLighting">True when the lighting mode is rare for this device.</param>
    public sealed record ClipSignals(bool BitrateCollapsed, int ClippedPeakCount, double? NoiseFloorRiseDb, bool UnusualLighting);

    /// <summary>The red flags raised for a clip, and whether they point to jamming.</summary>
    public sealed record RedFlagReport(IReadOnlyList<RedFlag> Flags, bool JammingSuspected);

    /// <summary>
    /// Turns a clip's signals into red flags. Jamming is suspected when video bitrate collapse and audio clipping occur
    /// together (the strongest pairing), or when three or more flags are raised.
    /// </summary>
    public static class RedFlagEvaluator
    {
        /// <summary>Clipped peaks at or above this count raise <see cref="RedFlag.ClippedAudio"/>. Tune against real footage.</summary>
        public const int ClippedPeakThreshold = 10;

        /// <summary>A noise floor this many dB above baseline raises <see cref="RedFlag.ElevatedNoiseFloor"/>.</summary>
        public const double NoiseFloorRiseThresholdDb = 10.0;

        /// <summary>Flags raised at or above this count suspect jamming on their own.</summary>
        public const int JammingFlagCount = 3;

        /// <summary>Evaluates a clip's signals into its red flags.</summary>
        public static RedFlagReport Evaluate(ClipSignals signals)
        {
            var flags = new List<RedFlag>();
            if (signals.BitrateCollapsed)
            {
                flags.Add(RedFlag.BitrateCollapse);
            }

            if (signals.ClippedPeakCount >= ClippedPeakThreshold)
            {
                flags.Add(RedFlag.ClippedAudio);
            }

            if (signals.NoiseFloorRiseDb is { } rise && rise >= NoiseFloorRiseThresholdDb)
            {
                flags.Add(RedFlag.ElevatedNoiseFloor);
            }

            if (signals.UnusualLighting)
            {
                flags.Add(RedFlag.UnusualLighting);
            }

            bool strongPair = flags.Contains(RedFlag.BitrateCollapse) && flags.Contains(RedFlag.ClippedAudio);
            return new RedFlagReport(flags, JammingSuspected: strongPair || flags.Count >= JammingFlagCount);
        }
    }
}
