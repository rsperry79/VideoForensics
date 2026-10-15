namespace VideoForensics.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of live download progress for <c>DownloadProgressPanel</c>. The panel
    /// subscribes instead of polling, so it does not depend on how a host gets its data.
    /// <para>
    /// Local hosts (WebApp) sample the download service and emit only when the state changes or activity
    /// lines arrive. Remote hosts (MAUI) forward the server's realtime stream. Both replay the most recent
    /// snapshot to a new subscriber, and emit nothing before the first sample.
    /// </para>
    /// </summary>
    public interface IDownloadProgressSource
    {
        /// <summary>Stream of progress snapshots. Subscribers must dispose their subscription.</summary>
        IObservable<DownloadProgressSnapshot> Progress { get; }
    }
}
