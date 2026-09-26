using System;
using System.Collections.Generic;
using System.Linq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Implementations
{
    /// <summary>
    /// Compares live-view RTP/RTCP telemetry samples against camera baselines to score interference likelihood.
    /// Uses a weighted composite of bitrate collapse, packet loss, and jitter deviation.
    /// </summary>
    public class LiveViewInterferenceScorer : ILiveViewInterferenceScorer
    {
        /// <summary>
        /// Scores a single telemetry sample against baseline metrics.
        /// </summary>
        public double? ScoreSample(
            LiveViewTelemetrySample sample,
            CameraBitrateBaseline? bucketBaseline,
            CameraBitrateBaseline? deviceGlobalFallback)
        {
            // Select the baseline to use: prefer bucket-specific, fall back to device-global, then null
            var baseline = bucketBaseline ?? deviceGlobalFallback;
            if (baseline == null)
            {
                return null;
            }

            // If all sample measurement fields are null, we cannot score
            if (sample.BitrateBps == null && sample.FractionLost == null && sample.JitterTicks == null)
            {
                return null;
            }

            // Compute the three deviation components, each roughly in [0,1]
            var bitrateComponent = ComputeBitrateComponent(sample, baseline);
            var lossComponent = ComputeLossComponent(sample, baseline);
            var jitterComponent = ComputeJitterComponent(sample, baseline);

            // Combine components that ARE present (skip nulls) into a single composite score.
            // Weight: bitrate 0.4 (primary signal for bandwidth constraint detection),
            //         loss 0.2 (can occur for many reasons, lower weight),
            //         jitter 0.4 (4x baseline jitter is very strong RF interference indicator).
            // If some components are missing, renormalize the weights.
            var components = new List<(double score, double weight)>();

            if (bitrateComponent.HasValue)
                components.Add((bitrateComponent.Value, 0.4));

            if (lossComponent.HasValue)
                components.Add((lossComponent.Value, 0.2));

            if (jitterComponent.HasValue)
                components.Add((jitterComponent.Value, 0.4));

            if (components.Count == 0)
                return null;

            // Renormalize weights if some components are missing
            var totalWeight = components.Sum(c => c.weight);
            var normalizedComponents = components.Select(c => (score: c.score, weight: c.weight / totalWeight)).ToList();

            // Compute weighted average
            var compositeScore = normalizedComponents.Sum(c => c.score * c.weight);

            // Clamp to [0,1]
            return Math.Clamp(compositeScore, 0.0, 1.0);
        }

        /// <summary>
        /// Computes the bitrate deviation component.
        /// Measures how far below baseline the sample bitrate is, in units of baseline std dev.
        /// A bitrate ABOVE baseline should NOT count as degraded (clamped to 0).
        /// If std dev is near-zero, treat any deviation below median as moderate-to-high score.
        /// </summary>
        private double? ComputeBitrateComponent(LiveViewTelemetrySample sample, CameraBitrateBaseline baseline)
        {
            if (sample.BitrateBps == null)
                return null;

            var deviation = baseline.MedianBitrateBps - sample.BitrateBps.Value;

            // Bitrate above baseline = 0 degradation
            if (deviation <= 0)
                return 0.0;

            // If std dev is effectively zero, use a threshold-based approach
            // Any significant deviation below median maps to 0.5 (moderate score)
            if (baseline.StdDevBitrateBps < 1)
            {
                // Conservative: a 10% drop from median when there's no variance data = 0.5 score
                var percentageDrop = (double)deviation / baseline.MedianBitrateBps;
                return Math.Min(percentageDrop * 5.0, 1.0); // Maps 10% drop to 0.5, 20%+ to clamped 1.0
            }

            // Normal case: express deviation in units of std dev and clamp
            var stdDevsBelow = deviation / baseline.StdDevBitrateBps;

            // Map std devs to a score curve (non-linear to emphasize sharp drops)
            // 1 std dev = 0.2 score, 2 = 0.4, 3+ = 1.0
            var bitrateScore = Math.Min(stdDevsBelow / 5.0, 1.0);

            return bitrateScore;
        }

        /// <summary>
        /// Computes the packet loss deviation component.
        /// Measures how far sample FractionLost exceeds baseline, clamped to [0,1].
        /// FractionLost is stored as a percentage (0-100), not a fraction (0-255).
        /// </summary>
        private double? ComputeLossComponent(LiveViewTelemetrySample sample, CameraBitrateBaseline baseline)
        {
            if (sample.FractionLost == null)
                return null;

            // FractionLost is stored as a percentage (0-100)
            var sampleLossPercent = sample.FractionLost.Value; // Already a percentage
            var exceedance = sampleLossPercent - baseline.MedianFractionLost;

            // Clamp to [0,1]: negative exceedance (better than baseline) = 0
            // Since loss is a percentage (0-100), we need to normalize to [0,1]
            return Math.Clamp(exceedance / 100.0, 0.0, 1.0);
        }

        /// <summary>
        /// Computes the jitter deviation component.
        /// Measures how far sample JitterTicks exceeds baseline, clamped to [0,1].
        /// Jitter is in RTP timestamp units (typically 1/90000 sec for video).
        /// </summary>
        private double? ComputeJitterComponent(LiveViewTelemetrySample sample, CameraBitrateBaseline baseline)
        {
            if (sample.JitterTicks == null)
                return null;

            var exceedance = sample.JitterTicks.Value - baseline.MedianJitterTicks;

            // Negative exceedance (less jitter than baseline) = 0 degradation
            if (exceedance <= 0)
                return 0.0;

            // Map jitter exceedance to [0,1]
            // If exceedance is 2x baseline, score = 0.5; 4x = 1.0
            var jitterScore = Math.Min((double)exceedance / (baseline.MedianJitterTicks * 4.0), 1.0);

            return jitterScore;
        }
    }
}
