using System.Globalization;
using System.Text.RegularExpressions;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Audio measurements for one clip, the inputs to the audio red flags.</summary>
    /// <param name="ClippedPeakCount">Samples at full scale (clipping). Zero unless the clip's peak reaches about 0 dBFS.</param>
    /// <param name="NoiseFloorDb">Noise floor in dB at the end of the clip; null if not reported.</param>
    /// <param name="SilentGapCount">Silent gaps at or above the minimum dropout length.</param>
    /// <param name="SilenceFraction">Total silent time as a fraction of the clip; null if the duration is unknown.</param>
    public sealed record AudioClipStats(int ClippedPeakCount, double? NoiseFloorDb, int SilentGapCount, double? SilenceFraction);

    /// <summary>
    /// Parses ffmpeg output from <c>astats=metadata=1</c> (stdout metadata lines) and <c>silencedetect</c> (stderr log lines).
    /// </summary>
    public static class AudioStatsParser
    {
        /// <summary>Silent gaps shorter than this are ignored; they are normal pauses, not dropouts.</summary>
        public const double MinimumGapSeconds = 0.2;

        private static readonly Regex PeakCount = new(@"lavfi\.astats\.Overall\.Peak_count=(?<v>\d+)", RegexOptions.Compiled);
        private static readonly Regex PeakLevel = new(@"lavfi\.astats\.Overall\.Peak_level=(?<v>-?\d+(?:\.\d+)?|-inf)", RegexOptions.Compiled);

        /// <summary>A peak this close to 0 dBFS is treated as reaching full scale.</summary>
        public const double FullScaleToleranceDb = 0.1;

        private static readonly Regex NoiseFloor = new(@"lavfi\.astats\.Overall\.Noise_floor=(?<v>-?\d+(?:\.\d+)?)", RegexOptions.Compiled);
        private static readonly Regex SilenceDuration = new(@"silence_duration: (?<v>\d+(?:\.\d+)?)", RegexOptions.Compiled);

        /// <summary>Returns the clip's audio stats, or null when there is no astats output at all.</summary>
        public static AudioClipStats? Parse(string astatsOutput, string silencedetectOutput, double? durationSeconds)
        {
            var peaks = PeakCount.Matches(astatsOutput).Select(m => int.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture)).ToList();
            if (peaks.Count == 0)
            {
                return null;
            }

            var floors = NoiseFloor.Matches(astatsOutput).Select(m => double.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture)).ToList();
            var gaps = SilenceDuration.Matches(silencedetectOutput)
                .Select(m => double.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture))
                .ToList();

            double silentSeconds = gaps.Sum();
            double? fraction = durationSeconds is > 0 ? silentSeconds / durationSeconds.Value : null;

            // Peak_count counts samples at the clip's own peak level, so on digital silence it is huge but meaningless.
            // Only a peak at full scale means clipping.
            // The last reported peak level that is not -inf (silence); null when none was reported.
            double? peakLevel = PeakLevel.Matches(astatsOutput)
                .Select(m => m.Groups["v"].Value)
                .Where(v => v != "-inf")
                .Select(v => (double?)double.Parse(v, CultureInfo.InvariantCulture))
                .LastOrDefault();
            bool atFullScale = peakLevel is { } level && level >= -FullScaleToleranceDb;

            return new AudioClipStats(
                ClippedPeakCount: atFullScale ? peaks.Max() : 0,
                NoiseFloorDb: floors.Count == 0 ? null : floors[^1],
                SilentGapCount: gaps.Count(g => g >= MinimumGapSeconds),
                SilenceFraction: fraction);
        }
    }
}
