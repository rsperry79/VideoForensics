namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Calendar season, used to bucket baselines so seasonal lighting and foliage shifts stay in their own bucket.</summary>
    public enum Season
    {
        Winter,
        Spring,
        Summer,
        Autumn
    }

    /// <summary>Season helpers.</summary>
    public static class SeasonExtensions
    {
        /// <summary>Maps a month (1-12) to a season for the given hemisphere; southern seasons are six months out of phase.</summary>
        public static Season FromMonth(int month, Hemisphere hemisphere)
        {
            Season northern = month switch
            {
                12 or 1 or 2 => Season.Winter,
                3 or 4 or 5 => Season.Spring,
                6 or 7 or 8 => Season.Summer,
                _ => Season.Autumn
            };

            return hemisphere == Hemisphere.Northern ? northern : (Season)(((int)northern + 2) % 4);
        }
    }

    /// <summary>One clip's bitrate, with the lighting mode and season it was captured in, and whether it raised a red flag.</summary>
    public sealed record BaselineSample(long BitrateBps, LightingMode Mode, Season Season, bool Flagged);

    /// <summary>Baseline bucket: lighting mode and season.</summary>
    public readonly record struct BaselineBucket(LightingMode Mode, Season? Season);

    /// <summary>
    /// A versioned set of baselines. Each analysed clip records the version it was judged against, so a later review can see
    /// exactly which baseline the system used.
    /// </summary>
    public sealed class BaselineSnapshot
    {
        private readonly IReadOnlyDictionary<BaselineBucket, BitrateBaseline> _buckets;

        /// <summary>Creates a snapshot. Use <see cref="SeasonalBaselineBuilder.Build"/> rather than calling this directly.</summary>
        internal BaselineSnapshot(int version, IReadOnlyDictionary<BaselineBucket, BitrateBaseline> buckets)
        {
            Version = version;
            _buckets = buckets;
        }

        /// <summary>Snapshot version, increasing each time baselines are rebuilt.</summary>
        public int Version { get; }

        /// <summary>
        /// Baseline for a lighting mode and season; falls back to the mode-wide bucket when the season has too few clips.
        /// Null when neither exists.
        /// </summary>
        public BitrateBaseline? Get(LightingMode mode, Season season)
        {
            return _buckets.TryGetValue(new BaselineBucket(mode, season), out BitrateBaseline? seasonal)
                ? seasonal
                : _buckets.TryGetValue(new BaselineBucket(mode, null), out BitrateBaseline? modeWide) ? modeWide : null;
        }
    }

    /// <summary>
    /// Builds baselines from clips that raised no red flags, so a sustained fault cannot train the baseline to accept
    /// itself. Clips are bucketed by season, and a season with too few clips falls back to all seasons for the mode.
    /// </summary>
    public static class SeasonalBaselineBuilder
    {
        /// <summary>Minimum unflagged clips a bucket needs before its baseline is used.</summary>
        public const int MinimumBucketSamples = 10;

        /// <summary>
        /// Builds a snapshot from the samples. The version is a hash of the clean samples, so identical history always gives
        /// the same version and any change to the baseline gives a new one.
        /// </summary>
        public static BaselineSnapshot Build(IReadOnlyList<BaselineSample> samples)
        {
            var clean = samples.Where(s => !s.Flagged).ToList();
            var buckets = new Dictionary<BaselineBucket, BitrateBaseline>();

            foreach (IGrouping<LightingMode, BaselineSample> byMode in clean.GroupBy(s => s.Mode))
            {
                foreach (IGrouping<Season, BaselineSample> bySeason in byMode.GroupBy(s => s.Season))
                {
                    AddIfEnough(buckets, new BaselineBucket(byMode.Key, bySeason.Key), bySeason.Select(s => s.BitrateBps).ToList());
                }

                AddIfEnough(buckets, new BaselineBucket(byMode.Key, null), byMode.Select(s => s.BitrateBps).ToList());
            }

            return new BaselineSnapshot(ContentVersion(clean), buckets);
        }

        /// <summary>Stable 31-bit hash of the sorted sample contents.</summary>
        private static int ContentVersion(IEnumerable<BaselineSample> clean)
        {
            string canonical = string.Join("\n", clean
                .Select(s => $"{s.BitrateBps}|{s.Mode}|{s.Season}")
                .OrderBy(x => x, StringComparer.Ordinal));
            byte[] digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
            return BitConverter.ToInt32(digest, 0) & int.MaxValue;
        }

        private static void AddIfEnough(Dictionary<BaselineBucket, BitrateBaseline> buckets, BaselineBucket key, List<long> bitrates)
        {
            if (bitrates.Count < MinimumBucketSamples)
            {
                return;
            }

            var baseline = BitrateBaseline.From(bitrates);
            if (baseline is not null)
            {
                buckets[key] = baseline;
            }
        }
    }
}
