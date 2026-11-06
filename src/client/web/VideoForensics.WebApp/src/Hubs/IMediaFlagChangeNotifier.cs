namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>
    /// Announces that a media flag was reviewed or re-opened. The flag endpoints call it after a successful write, so every open
    /// review page, in this process or on a remote client, hears about the change.
    /// </summary>
    public interface IMediaFlagChangeNotifier
    {
        /// <summary>Publishes the change. A failed push must not fail the write that caused it.</summary>
        Task MediaFlagChangedAsync(Guid flagId, Guid deviceId, CancellationToken ct);
    }
}
