using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Value-based comparison for <see cref="DownloadProgressDto"/> send-on-change.
    /// <para>
    /// Compared: progress counters, current device index/total/name, last error, remaining reason,
    /// and pre-scan counts. Pre-scan counts are included because they change during the scan phase
    /// and the UI's "Items" column depends on them. Excluded: Activity (transient; handled by
    /// <see cref="ShouldSend"/>).
    /// </para>
    /// <para>
    /// PreScanCounts is compared by content, not reference. The service may return a new dictionary
    /// on every call, so a reference comparison would report a change on every tick.
    /// </para>
    /// </summary>
    public sealed class DownloadProgressChangeDetector : IDownloadProgressChangeDetector
    {
        /// <inheritdoc />
        public bool HasChanged(DownloadProgressDto? previous, DownloadProgressDto current)
        {
            if (previous is null)
            {
                return true;
            }

            return !previous.Progress.Equals(current.Progress)
                || previous.CurrentDeviceIndex != current.CurrentDeviceIndex
                || previous.CurrentDeviceTotal != current.CurrentDeviceTotal
                || previous.CurrentDeviceName != current.CurrentDeviceName
                || previous.LastError != current.LastError
                || previous.RemainingReason != current.RemainingReason
                || !PreScanCountsEqual(previous.PreScanCounts, current.PreScanCounts);
        }

        /// <inheritdoc />
        public bool ShouldSend(DownloadProgressDto? previous, DownloadProgressDto current)
        {
            return current.Activity.Count > 0 || HasChanged(previous, current);
        }

        private static bool PreScanCountsEqual(
            IReadOnlyDictionary<string, int> left,
            IReadOnlyDictionary<string, int> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, int> entry in left)
            {
                if (!right.TryGetValue(entry.Key, out int value) || value != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
