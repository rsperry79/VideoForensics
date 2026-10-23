using System.Globalization;
using System.Text.RegularExpressions;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>How a clip was lit: IR (grayscale, dark), colour night (floodlit colour, dark), or day/HDR (bright).</summary>
    public enum LightingMode
    {
        /// <summary>Infrared night footage: grayscale and dark.</summary>
        Ir,

        /// <summary>Colour night vision: colour kept by a floodlight, but dark.</summary>
        ColorNight,

        /// <summary>Visible-light footage, including HDR.</summary>
        Day
    }

    /// <summary>Mean colour saturation (SATAVG) and mean luma (YAVG, 0-255) of a clip.</summary>
    public sealed record ClipColorStats(double MeanSaturation, double MeanLuma);

    /// <summary>Parses ffmpeg <c>signalstats</c> output into a clip's mean saturation and luma.</summary>
    public static class SaturationAnalysis
    {
        private static readonly Regex SatAvg = new(@"lavfi\.signalstats\.SATAVG=(?<value>-?\d+(?:\.\d+)?)", RegexOptions.Compiled);
        private static readonly Regex YAvg = new(@"lavfi\.signalstats\.YAVG=(?<value>-?\d+(?:\.\d+)?)", RegexOptions.Compiled);

        /// <summary>Means of the per-frame SATAVG and YAVG values; null when either was not reported.</summary>
        public static ClipColorStats? ParseStats(string signalstatsOutput)
        {
            List<double> saturation = Values(SatAvg, signalstatsOutput);
            List<double> luma = Values(YAvg, signalstatsOutput);
            return saturation.Count == 0 || luma.Count == 0 ? null : new ClipColorStats(saturation.Average(), luma.Average());
        }

        private static List<double> Values(Regex pattern, string text)
        {
            return pattern.Matches(text)
                .Select(m => double.Parse(m.Groups["value"].Value, CultureInfo.InvariantCulture))
                .ToList();
        }
    }

    /// <summary>
    /// Labels a clip from its colour and brightness. Colour alone cannot separate IR from colour night: both can be dark,
    /// and colour night keeps colour. Brightness separates them from day.
    /// </summary>
    public static class LightingClassifier
    {
        /// <summary>Saturation below which a clip is grayscale.</summary>
        public const double GrayscaleSaturationCeiling = 8.0;

        /// <summary>Mean luma (0-255) below which a clip is dark. Tune against real footage: it is an estimate.</summary>
        public const double DarkLumaCeiling = 60.0;

        /// <summary>Classifies a clip. Bright footage is day regardless of colour; dark footage is IR or colour night.</summary>
        public static LightingMode Classify(ClipColorStats stats)
        {
            return stats.MeanLuma >= DarkLumaCeiling
                ? LightingMode.Day
                : stats.MeanSaturation < GrayscaleSaturationCeiling ? LightingMode.Ir : LightingMode.ColorNight;
        }
    }

    /// <summary>Median bitrate of a device's clips in one lighting mode, and how many clips it was computed from.</summary>
    public sealed record BitrateBaseline(long MedianBps, int SampleCount)
    {
        /// <summary>Median of the sample bitrates; null when there are none.</summary>
        public static BitrateBaseline? From(IReadOnlyList<long> bitrates)
        {
            if (bitrates.Count == 0)
            {
                return null;
            }

            var sorted = bitrates.OrderBy(b => b).ToList();
            int mid = sorted.Count / 2;
            long median = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
            return new BitrateBaseline(median, sorted.Count);
        }
    }

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
