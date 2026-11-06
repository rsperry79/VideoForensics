using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Providers.Core
{
    /// <summary>
    /// Maps stored snapshot attempts to timeline stills. Only a device's 200 attempts whose media file exists become stills.
    /// A non-200 attempt yields no still but ends the hold of the still before it: outages are gaps, not held frames.
    /// Each still is held until the earlier of the next still or the next failed attempt.
    /// </summary>
    public sealed class SnapshotStillMapper : ISnapshotStillMapper
    {
        /// <inheritdoc />
        public IReadOnlyList<TimelineStill> MapCurrentStills(SnapshotStillMapRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Records);
            if (request.LastStillHold <= TimeSpan.Zero)
            {
                throw new ArgumentException("The last still must have a positive hold.", nameof(request));
            }

            // Existence is checked here, not left to the builder, so one missing file is a gap instead of a failed build.
            var current = new List<(DateTimeOffset CapturedAt, string ImagePath)>();
            var failures = new List<DateTimeOffset>();
            foreach (SnapshotStillRecord record in request.Records)
            {
                if (record is null || record.DeviceId != request.DeviceId)
                {
                    continue;
                }

                // Failed attempts never become stills, but they are kept to end the hold of the still before them.
                if (record.HttpStatus != 200)
                {
                    failures.Add(record.CapturedAt);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.MediaFilePath))
                {
                    continue;
                }

                string imagePath = Path.GetFullPath(record.MediaFilePath);
                if (File.Exists(imagePath))
                {
                    current.Add((record.CapturedAt, imagePath));
                }
            }

            // OrderBy is stable, so captures at the same instant keep their input order.
            var ordered = current.OrderBy(c => c.CapturedAt).ToList();
            failures.Sort();
            var stills = new List<TimelineStill>(ordered.Count);
            int nextFailure = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                DateTimeOffset start = ordered[i].CapturedAt;

                // Skip failures at or before this still: only a failure strictly after it can end its hold.
                while (nextFailure < failures.Count && failures[nextFailure] <= start)
                {
                    nextFailure++;
                }

                // A still is held until the earlier of the next still or the next failed attempt. The last still with
                // no failure after it uses the caller's hold.
                DateTimeOffset? end = i + 1 < ordered.Count ? ordered[i + 1].CapturedAt : null;
                if (nextFailure < failures.Count && (end is null || failures[nextFailure] < end.Value))
                {
                    end = failures[nextFailure];
                }

                TimeSpan hold = end is null ? request.LastStillHold : end.Value - start;

                // A zero hold means a later capture at the same instant replaces this one. The builder rejects non-positive holds.
                if (hold <= TimeSpan.Zero)
                {
                    continue;
                }

                stills.Add(new TimelineStill(ordered[i].ImagePath, start, hold));
            }

            return stills;
        }
    }
}
