namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Generates thumbnails for archived videos that do not have one yet.</summary>
    public interface IMediaThumbnailBackfill
    {
        /// <summary>Checks every non-purged video and ensures it has a thumbnail.</summary>
        Task<MediaThumbnailBackfillResult> BackfillAsync(CancellationToken ct);
    }

    /// <summary>Outcome of one backfill pass over the media archive.</summary>
    /// <param name="Checked">Videos with a file on disk that were checked for a thumbnail.</param>
    /// <param name="Available">Checked videos that have a thumbnail after the pass.</param>
    /// <param name="Unavailable">Checked videos whose thumbnail could not be made.</param>
    /// <param name="MissingFile">Videos whose media file is gone from disk, so no thumbnail is possible.</param>
    public sealed record MediaThumbnailBackfillResult(int Checked, int Available, int Unavailable, int MissingFile);
}
