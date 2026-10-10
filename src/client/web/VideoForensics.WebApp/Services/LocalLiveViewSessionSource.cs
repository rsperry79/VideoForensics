using System.Reactive.Linq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Contracts;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// <see cref="ILiveViewSessionSource"/> for the WebApp. The WebApp runs live view in-process and is not a client
    /// of its own live hub, so it has no push channel to offer. It reports <see cref="IsConnected"/> as false, and the
    /// LiveView page polls for activation as it did before.
    /// </summary>
    public sealed class LocalLiveViewSessionSource : ILiveViewSessionSource
    {
        /// <inheritdoc />
        public IObservable<LiveViewSession> SessionChanged { get; } = Observable.Never<LiveViewSession>();

        /// <inheritdoc />
        public bool IsConnected => false;

        /// <inheritdoc />
        public Task SubscribeAsync(Guid sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task UnsubscribeAsync(Guid sessionId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
