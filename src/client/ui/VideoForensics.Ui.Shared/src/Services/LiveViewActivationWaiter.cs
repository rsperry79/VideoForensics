using System.Diagnostics;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Contracts;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Waits for a live-view session to become <see cref="LiveViewSessionState.Active"/> after it is started.
    /// The push from <see cref="ILiveViewSessionSource"/> is the primary signal. Polling is the fallback, used when
    /// the source is not connected or the subscription fails.
    /// </summary>
    /// <remarks>
    /// Why a single read after subscribing: pushes are not replayed, so a session that turned Active between the
    /// start call and the subscription would otherwise be missed and the wait would run to the timeout.
    /// Why a final read on timeout: a connected hub can stay quiet and still have the session active. One read
    /// there keeps a lost push from failing a session that is really up.
    /// </remarks>
    public sealed class LiveViewActivationWaiter
    {
        /// <summary>How long to wait for activation. Matches the page's former 30 x 500 ms poll.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

        // The poll interval of the fallback path. Kept at the page's former 500 ms.
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Returns the session once it is Active, or null if it is not Active before <paramref name="timeout"/>.
        /// </summary>
        /// <param name="sessionId">The session to wait for. Pushes for other sessions are ignored.</param>
        /// <param name="source">The push source, or null when none is available.</param>
        /// <param name="getActiveAsync">Reads the device's active session. Used for the race guard, the poll fallback and the final read.</param>
        /// <param name="timeout">The longest time to wait.</param>
        /// <param name="cancellationToken">Cancels the wait. Cancellation throws <see cref="OperationCanceledException"/>.</param>
        /// <returns>The Active session, or null if the timeout passed first.</returns>
        public async Task<LiveViewSession?> WaitForActiveAsync(
            Guid sessionId,
            ILiveViewSessionSource? source,
            Func<CancellationToken, Task<LiveViewSession?>> getActiveAsync,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(getActiveAsync);

            if (source is null || !source.IsConnected)
            {
                return await PollUntilActiveAsync(getActiveAsync, timeout, cancellationToken);
            }

            // Subscribe to the stream before asking the host for pushes. Pushes are not replayed, so attaching
            // after the request would drop an early push.
            var activated = new TaskCompletionSource<LiveViewSession>(TaskCreationOptions.RunContinuationsAsynchronously);
            using IDisposable observer = source.SessionChanged.Subscribe(
                session =>
                {
                    if (session.Id == sessionId && session.State == LiveViewSessionState.Active)
                    {
                        _ = activated.TrySetResult(session);
                    }
                },
                // A failed stream ends pushes for this subscriber. The timeout and final read below still cover it.
                onError: _ => { });

            try
            {
                await source.SubscribeAsync(sessionId, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The host could not deliver pushes for this session. Polling is the fallback, not an error.
                return await PollUntilActiveAsync(getActiveAsync, timeout, cancellationToken);
            }

            LiveViewSession? current = await getActiveAsync(cancellationToken);
            if (current?.State == LiveViewSessionState.Active)
            {
                return current;
            }

            Task winner = await Task.WhenAny(activated.Task, Task.Delay(timeout, cancellationToken));
            if (winner == activated.Task)
            {
                return await activated.Task;
            }

            // Stops a cancelled wait from reaching the final read; the caller gets OperationCanceledException.
            cancellationToken.ThrowIfCancellationRequested();

            current = await getActiveAsync(cancellationToken);
            return current?.State == LiveViewSessionState.Active ? current : null;
        }

        // Fallback poll. Polls every PollInterval until the deadline. The last wait is cut short so the total
        // never exceeds the timeout.
        private static async Task<LiveViewSession?> PollUntilActiveAsync(
            Func<CancellationToken, Task<LiveViewSession?>> getActiveAsync,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                TimeSpan remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    return null;
                }

                await Task.Delay(remaining < PollInterval ? remaining : PollInterval, cancellationToken);

                LiveViewSession? current = await getActiveAsync(cancellationToken);
                if (current?.State == LiveViewSessionState.Active)
                {
                    return current;
                }
            }
        }
    }
}
