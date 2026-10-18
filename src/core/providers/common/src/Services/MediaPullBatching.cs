namespace VideoForensics.Core.Providers.Common.Services
{
    /// <summary>Splits a pull window into fixed-length batches, so a long backfill runs as a series of small pulls.</summary>
    public static class MediaPullBatching
    {
        /// <summary>
        /// Yields consecutive batches from <paramref name="start"/> to <paramref name="end"/>. Every batch is
        /// <paramref name="batchLength"/> long except the last, which ends exactly at <paramref name="end"/>.
        /// Yields nothing when start is not before end.
        /// </summary>
        public static IEnumerable<(DateTime Start, DateTime End)> Split(DateTime start, DateTime end, TimeSpan batchLength)
        {
            if (batchLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(batchLength), "Batch length must be positive.");
            }

            for (DateTime batchStart = start; batchStart < end;)
            {
                DateTime batchEnd = batchStart + batchLength < end ? batchStart + batchLength : end;
                yield return (batchStart, batchEnd);
                batchStart = batchEnd;
            }
        }
    }
}
