using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Default notifier for hosts with no live views. Does nothing.</summary>
    public sealed class NullEvidenceChangeNotifier : IEvidenceChangeNotifier
    {
        /// <inheritdoc />
        public Task EvidenceChangedAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
