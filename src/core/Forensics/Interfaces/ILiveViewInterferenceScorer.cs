using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Providers.Ring.Interfaces
{
    /// <summary>
    /// Scores live-view telemetry samples for potential RF interference or video degradation by comparing
    /// them against time-bucketed baseline statistics for the camera.
    /// </summary>
    public interface ILiveViewInterferenceScorer
    {
        /// <summary>
        /// Compares a live telemetry sample against the camera's time-bucketed baseline to produce a
        /// composite interference/degradation score in roughly [0,1] (clamped), where higher means more
        /// likely jammed. Falls back from the specific hour/weekend bucket baseline to a device-global
        /// baseline if the bucket has insufficient data, and returns null (not zero) if no baseline data
        /// exists at all - callers must not treat null as "no interference," only as "cannot score yet."
        /// </summary>
        /// <param name="sample">The live telemetry sample to score.</param>
        /// <param name="bucketBaseline">Baseline for the specific hour/weekend bucket, or null to use device global fallback.</param>
        /// <param name="deviceGlobalFallback">Device-wide fallback baseline, or null if no bucket baseline available.</param>
        /// <returns>
        /// A composite score in [0,1] where higher indicates more degradation/interference; null if no baseline
        /// data exists or if the sample has no telemetry data (all measurement fields null).
        /// </returns>
        double? ScoreSample(
            LiveViewTelemetrySample sample,
            CameraBitrateBaseline? bucketBaseline,
            CameraBitrateBaseline? deviceGlobalFallback);
    }
}
