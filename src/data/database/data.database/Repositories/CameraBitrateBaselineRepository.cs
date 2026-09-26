using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for camera bitrate baselines.</summary>
    public class CameraBitrateBaselineRepository : ICameraBitrateBaselineRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<CameraBitrateBaselineRepository> _logger;

        /// <summary>Initializes a new instance of the CameraBitrateBaselineRepository.</summary>
        public CameraBitrateBaselineRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<CameraBitrateBaselineRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets the baseline statistics for a specific hour/weekend bucket of a device.</summary>
        public async Task<CameraBitrateBaseline?> GetBucketAsync(Guid deviceId, int hourOfDay, bool isWeekend, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.CameraBitrateBaselines
                .FirstOrDefaultAsync(b => b.DeviceId == deviceId && b.HourOfDay == hourOfDay && b.IsWeekend == isWeekend, ct);
        }

        /// <summary>Gets the device-global fallback baseline (average across all buckets) for cold-start scenarios.</summary>
        public async Task<CameraBitrateBaseline?> GetDeviceGlobalAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            var baselines = await db.CameraBitrateBaselines
                .Where(b => b.DeviceId == deviceId)
                .ToListAsync(ct);

            if (baselines.Count == 0)
            {
                return null;
            }

            // Compute an artificial "global" baseline as the average across all buckets
            var global = new CameraBitrateBaseline
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                HourOfDay = -1,
                IsWeekend = false,
                SampleCount = (int)baselines.Average(b => b.SampleCount),
                MedianBitrateBps = (long)baselines.Average(b => b.MedianBitrateBps),
                StdDevBitrateBps = baselines.Average(b => b.StdDevBitrateBps),
                MedianFractionLost = baselines.Average(b => b.MedianFractionLost),
                MedianJitterTicks = baselines.Average(b => b.MedianJitterTicks),
                LastRecomputedAtUtc = DateTime.UtcNow
            };

            return global;
        }

        /// <summary>Recomputes and upserts the baseline statistics for a specific hour/weekend bucket from current telemetry.</summary>
        public async Task RecomputeBucketAsync(Guid deviceId, int hourOfDay, bool isWeekend, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                // Fetch all sessions for this device, then their samples for this hour/weekend bucket
                var deviceSessions = await db.LiveViewSessions
                    .Where(s => s.DeviceId == deviceId)
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                var samples = await db.LiveViewTelemetrySamples
                    .Where(t => deviceSessions.Contains(t.SessionId)
                        && t.CapturedAtUtc.Hour == hourOfDay
                        && ((t.CapturedAtUtc.DayOfWeek == DayOfWeek.Saturday || t.CapturedAtUtc.DayOfWeek == DayOfWeek.Sunday) == isWeekend))
                    .ToListAsync(ct);

                if (samples.Count == 0)
                {
                    _logger.LogInformation("No samples found for recomputing bucket: Device {DeviceId}, Hour {HourOfDay}, IsWeekend {IsWeekend}",
                        deviceId, hourOfDay, isWeekend);
                    return;
                }

                // Calculate statistics
                var bitrateValues = samples.Where(s => s.BitrateBps.HasValue).Select(s => s.BitrateBps.Value).ToList();
                var fractionLostValues = samples.Where(s => s.FractionLost.HasValue).Select(s => (double)s.FractionLost.Value).ToList();
                var jitterValues = samples.Where(s => s.JitterTicks.HasValue).Select(s => (double)s.JitterTicks.Value).ToList();

                long medianBitrate = bitrateValues.Count > 0 ? GetMedian(bitrateValues) : 0;
                double stdDevBitrate = bitrateValues.Count > 0 ? GetStdDev(bitrateValues.Select(v => (double)v).ToList()) : 0;
                double medianFractionLost = fractionLostValues.Count > 0 ? GetMedian(fractionLostValues) : 0;
                double medianJitter = jitterValues.Count > 0 ? GetMedian(jitterValues) : 0;

                // Find or create the baseline record
                CameraBitrateBaseline? existing = await db.CameraBitrateBaselines
                    .FirstOrDefaultAsync(b => b.DeviceId == deviceId && b.HourOfDay == hourOfDay && b.IsWeekend == isWeekend, ct);

                CameraBitrateBaseline baseline = existing ?? new CameraBitrateBaseline
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    HourOfDay = hourOfDay,
                    IsWeekend = isWeekend
                };

                baseline.SampleCount = samples.Count;
                baseline.MedianBitrateBps = medianBitrate;
                baseline.StdDevBitrateBps = stdDevBitrate;
                baseline.MedianFractionLost = medianFractionLost;
                baseline.MedianJitterTicks = medianJitter;
                baseline.LastRecomputedAtUtc = DateTime.UtcNow;

                if (existing == null)
                {
                    _ = db.CameraBitrateBaselines.Add(baseline);
                }
                else
                {
                    _ = db.CameraBitrateBaselines.Update(baseline);
                }

                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Recomputed bitrate baseline: Device {DeviceId}, Hour {HourOfDay}, IsWeekend {IsWeekend}, SampleCount {SampleCount}",
                    deviceId, hourOfDay, isWeekend, samples.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recomputing baseline bucket for device: {DeviceId}, Hour {HourOfDay}, IsWeekend {IsWeekend}",
                    deviceId, hourOfDay, isWeekend);
                throw;
            }
        }

        /// <summary>Lists all buckets meeting calibration criteria (undersample or stale).</summary>
        public async Task<IReadOnlyList<(Guid DeviceId, int HourOfDay, bool IsWeekend)>> ListBucketsNeedingCalibrationAsync(int minSamples, TimeSpan staleness, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            DateTime staleCutoff = DateTime.UtcNow - staleness;

            var bucketsNeedingCalibration = await db.CameraBitrateBaselines
                .Where(b => b.SampleCount < minSamples || b.LastRecomputedAtUtc < staleCutoff)
                .Select(b => new { b.DeviceId, b.HourOfDay, b.IsWeekend })
                .ToListAsync(ct);

            return bucketsNeedingCalibration
                .Select(b => (b.DeviceId, b.HourOfDay, b.IsWeekend))
                .ToList();
        }

        /// <summary>Calculates the median of a list of values.</summary>
        private static T GetMedian<T>(List<T> values) where T : IComparable<T>
        {
            if (values.Count == 0)
            {
                throw new ArgumentException("Cannot compute median of empty list");
            }

            var sorted = values.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;

            if (sorted.Count % 2 == 0)
            {
                // For even count, return the lower middle value
                return sorted[mid - 1];
            }

            return sorted[mid];
        }

        /// <summary>Calculates the standard deviation of a list of values.</summary>
        private static double GetStdDev(List<double> values)
        {
            if (values.Count < 2)
            {
                return 0;
            }

            double mean = values.Average();
            double sumOfSquaredDifferences = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumOfSquaredDifferences / (values.Count - 1));
        }
    }
}
