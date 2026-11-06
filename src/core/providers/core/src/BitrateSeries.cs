namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Video bitrate for one second of a clip.</summary>
    /// <param name="Second">Whole seconds from the start of the clip.</param>
    /// <param name="Kbps">Video bits in that second, in kilobits per second.</param>
    public sealed record BitrateSecond(int Second, long Kbps);

    /// <summary>
    /// Builds a per-second bitrate series from video packet times and sizes. Every second from zero to the last packet is
    /// present, so a quiet second shows as zero rather than disappearing from the chart.
    /// </summary>
    public static class BitrateSeries
    {
        /// <summary>Buckets each packet's bytes into the whole second it starts in. Packets before time zero are skipped.</summary>
        /// <param name="packets">Packet start time in seconds and packet size in bytes.</param>
        /// <returns>One entry per second from zero to the last packet, oldest first; empty when there are no packets.</returns>
        public static IReadOnlyList<BitrateSecond> FromPackets(IEnumerable<(double PtsSeconds, long SizeBytes)> packets)
        {
            var bytesPerSecond = new List<long>();
            foreach ((double pts, long size) in packets)
            {
                if (pts < 0)
                {
                    continue;
                }

                int second = (int)Math.Floor(pts);
                while (bytesPerSecond.Count <= second)
                {
                    bytesPerSecond.Add(0);
                }

                bytesPerSecond[second] += size;
            }

            var series = new List<BitrateSecond>(bytesPerSecond.Count);
            for (int second = 0; second < bytesPerSecond.Count; second++)
            {
                series.Add(new BitrateSecond(second, bytesPerSecond[second] * 8 / 1000));
            }

            return series;
        }
    }
}
