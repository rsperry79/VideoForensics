using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Core.Tools
{
    /// <summary>
    /// No-op <see cref="ILiveViewTelemetryPublisher"/> used when no real-time transport is registered.
    /// Keeps the orchestrator free of transport dependencies and makes publishing a safe no-op by default.
    /// </summary>
    public sealed class NullLiveViewTelemetryPublisher : ILiveViewTelemetryPublisher
    {
        /// <inheritdoc />
        public Task PublishSessionChangedAsync(LiveViewSession session, CancellationToken ct) => Task.CompletedTask;
    }
}
