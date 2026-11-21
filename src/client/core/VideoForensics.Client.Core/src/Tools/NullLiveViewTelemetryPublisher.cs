using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Core.Tools
{
    /// <summary>
    /// No-op <see cref="ILiveViewTelemetryPublisher"/> used when no real-time transport is registered.
    /// Keeps the orchestrator free of transport dependencies and makes publishing a safe no-op by default.
    /// </summary>
    public sealed class NullLiveViewTelemetryPublisher : ILiveViewTelemetryPublisher
    {
        /// <inheritdoc />
        public Task PublishSessionChangedAsync(LiveViewSessionDto session, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task PublishSampleAsync(LiveViewTelemetrySampleDto sample, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
