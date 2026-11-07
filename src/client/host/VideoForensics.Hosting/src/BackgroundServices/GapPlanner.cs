namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>One time-lapse segment: a half-open span [StartUtc, EndUtc) of one device's timeline.</summary>
    public sealed record GapSegment(DateTime StartUtc, DateTime EndUtc);

    /// <summary>The segments to build, and the cursor to store once they are written.</summary>
    public sealed record GapPlanResult(IReadOnlyList<GapSegment> Segments, DateTime NextCursorUtc);

    /// <summary>
    /// Plans one device's time-lapse segments. A closed gap runs from the cursor to the next event, split into segments of
    /// at most <c>maxSegment</c> with the remainder last. An open gap (no closing event yet) emits full segments only, so the
    /// remainder waits until the gap closes. A segment with fewer than two snapshots writes nothing, but the cursor still moves
    /// past it, so a quiet or failed stretch does not block later gaps.
    /// </summary>
    public static class GapPlanner
    {
        /// <param name="cursorUtc">End of the last planned span for this device.</param>
        /// <param name="horizonUtc">Nothing is planned past this instant (last clean pull, minus lag, and the snapshot sync).</param>
        /// <param name="closingEventsUtc">Event times that close gaps. Only times in (cursor, horizon] are used.</param>
        /// <param name="snapshotsUtc">Snapshot capture times for the device.</param>
        /// <param name="maxSegment">Longest allowed segment. Must be positive.</param>
        public static GapPlanResult Plan(
            DateTime cursorUtc,
            DateTime horizonUtc,
            IReadOnlyList<DateTime> closingEventsUtc,
            IReadOnlyList<DateTime> snapshotsUtc,
            TimeSpan maxSegment)
        {
            ArgumentNullException.ThrowIfNull(closingEventsUtc);
            ArgumentNullException.ThrowIfNull(snapshotsUtc);
            if (maxSegment <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSegment), "The max segment must be positive.");
            }

            var segments = new List<GapSegment>();
            if (cursorUtc >= horizonUtc)
            {
                return new GapPlanResult(segments, cursorUtc);
            }

            // Duplicates and out-of-order events are normalised: a gap is defined only by distinct, ordered boundaries.
            List<DateTime> closings = closingEventsUtc
                .Where(e => e > cursorUtc && e <= horizonUtc)
                .Distinct()
                .OrderBy(e => e)
                .ToList();

            DateTime cursor = cursorUtc;
            foreach (DateTime closing in closings)
            {
                // Closed gap: full segments, then the remainder.
                for (DateTime start = cursor; start < closing;)
                {
                    DateTime end = Min(start + maxSegment, closing);
                    AddIfEnoughSnapshots(segments, snapshotsUtc, start, end);
                    start = end;
                }

                cursor = closing;
            }

            // Open gap: only full segments until the horizon. The remainder stays for a later check.
            while (horizonUtc - cursor >= maxSegment)
            {
                DateTime end = cursor + maxSegment;
                AddIfEnoughSnapshots(segments, snapshotsUtc, cursor, end);
                cursor = end;
            }

            return new GapPlanResult(segments, cursor);
        }

        private static void AddIfEnoughSnapshots(List<GapSegment> segments, IReadOnlyList<DateTime> snapshotsUtc, DateTime start, DateTime end)
        {
            int count = 0;
            foreach (DateTime t in snapshotsUtc)
            {
                if (t >= start && t < end)
                {
                    count++;
                    if (count >= 2)
                    {
                        segments.Add(new GapSegment(start, end));
                        return;
                    }
                }
            }
        }

        private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
    }
}
