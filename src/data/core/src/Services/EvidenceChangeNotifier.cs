namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Announces that a device's evidence changed (a media item or event was written), so live views can reload.
    /// Hosts that serve no live views use <see cref="NullEvidenceChangeNotifier"/>; the WebApp replaces it with a SignalR push.
    /// Implementations must not throw: the write that triggered the call has already succeeded.
    /// </summary>
    public interface IEvidenceChangeNotifier
    {
        /// <summary>Reports that evidence for <paramref name="deviceId"/> changed.</summary>
        Task EvidenceChangedAsync(Guid deviceId, CancellationToken ct);
    }

    /// <summary>Default notifier for hosts with no live views. Does nothing.</summary>
    public sealed class NullEvidenceChangeNotifier : IEvidenceChangeNotifier
    {
        /// <inheritdoc />
        public Task EvidenceChangedAsync(Guid deviceId, CancellationToken ct) => Task.CompletedTask;
    }
}
