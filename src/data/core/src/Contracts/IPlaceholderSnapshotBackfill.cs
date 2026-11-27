namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Marks existing repeat placeholder snapshots (a frame identical to the device's previous snapshot).</summary>
    public interface IPlaceholderSnapshotBackfill
    {
        /// <summary>
        /// Marks every repeat snapshot placeholder in batches until none remain. Returns how many rows were marked in this run.
        /// The first frame of a run of identical frames is kept unmarked. Files are not touched and no row is deleted. A second
        /// run marks nothing.
        /// </summary>
        Task<int> BackfillAsync(CancellationToken ct);
    }
}
