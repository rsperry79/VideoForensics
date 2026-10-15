using System.Globalization;
using System.Text.RegularExpressions;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Whether a clip was captured with the IR illuminator (grayscale) or in visible light (day/HDR).</summary>
    public enum LightingMode
    {
        /// <summary>Infrared night footage: effectively grayscale.</summary>
        Ir,

        /// <summary>Visible-light footage, including HDR.</summary>
        Day
    }

    /// <summary>Parses ffmpeg <c>signalstats</c> output into a mean colour saturation for a clip.</summary>
    public static class SaturationAnalysis
    {
        private static readonly Regex SatAvg = new(@"lavfi\.signalstats\.SATAVG=(?<value>-?\d+(?:\.\d+)?)", RegexOptions.Compiled);

        /// <summary>Mean of the per-frame SATAVG values; null when none were reported.</summary>
        public static double? ParseMeanSaturation(string signalstatsOutput)
        {
            var values = SatAvg.Matches(signalstatsOutput)
                .Select(m => double.Parse(m.Groups["value"].Value, CultureInfo.InvariantCulture))
                .ToList();

            return values.Count == 0 ? null : values.Average();
        }
    }

    /// <summary>Labels a clip IR or Day from its mean colour saturation. IR footage is effectively grayscale.</summary>
    public static class LightingClassifier
    {
        /// <summary>Saturation below which a clip is treated as grayscale IR footage.</summary>
        public const double IrSaturationCeiling = 8.0;

        /// <summary>Classifies a clip from its mean saturation (SATAVG).</summary>
        public static LightingMode Classify(double meanSaturation) =>
            meanSaturation < IrSaturationCeiling ? LightingMode.Ir : LightingMode.Day;
    }

    /// <summary>Median bitrate of a device's clips in one lighting mode, and how many clips it was computed from.</summary>
    public sealed record BitrateBaseline(long MedianBps, int SampleCount);

    /// <summary>Whether a clip's bitrate is low enough, against its own lighting-mode baseline, to suspect interference.</summary>
    public sealed record InterferenceAssessment(bool InterferenceSuspected, long? BaselineBps);

    /// <summary>
    /// Flags a clip whose bitrate has collapsed well below the baseline of clips shot in the same lighting mode.
    /// Comparing across modes would be wrong: IR footage legitimately carries a different bitrate than day/HDR.
    /// </summary>
    public static class InterferenceAssessor
    {
        /// <summary>Minimum clips a baseline needs before a drop against it can be called interference.</summary>
        public const int MinimumBaselineSamples = 10;

        /// <summary>A clip below this fraction of its baseline is suspected interference.</summary>
        public const double SuspectedFraction = 0.6;

        /// <summary>Assesses a clip's bitrate against the same-mode baseline; null baseline means no judgement.</summary>
        public static InterferenceAssessment Assess(long bitrateBps, BitrateBaseline? baseline)
        {
            if (baseline is null)
            {
                return new InterferenceAssessment(InterferenceSuspected: false, BaselineBps: null);
            }

            bool trustworthy = baseline.SampleCount >= MinimumBaselineSamples;
            bool collapsed = bitrateBps < baseline.MedianBps * SuspectedFraction;
            return new InterferenceAssessment(trustworthy && collapsed, baseline.MedianBps);
        }
    }
}
