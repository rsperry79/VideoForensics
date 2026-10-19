namespace VideoForensics.Data.Core.Services
{
    /// <summary>Settings for snapshot timelines. Bound from the <c>SnapshotTimeline</c> configuration section when one is set.</summary>
    public sealed class SnapshotTimelineOptions
    {
        /// <summary>
        /// Directory that stored snapshot media paths are relative to. This is the only place the default lives:
        /// callers take the root from these options.
        /// </summary>
        public string SnapshotRoot { get; set; } = @"C:\ProgramData\VideoForensics\Snapshots";
    }
}
