namespace VideoForensics.Data.Common.Entities
{
    /// <summary>What a download job fetches for its account.</summary>
    public enum DownloadJobKind
    {
        /// <summary>Video clips.</summary>
        Videos = 0,

        /// <summary>Snapshot stills.</summary>
        Snapshots = 1,
    }
}
