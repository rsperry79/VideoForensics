using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Diagnostics;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Watches the background loops' heartbeats and logs an error when one stops ticking. It alerts rather than restarting,
    /// because a hung loop can hold resources a restart would not release. Each stale loop is logged once, and logged again
    /// only after it beats and goes stale again.
    /// </summary>
    public sealed class ThreadWatchdogService : BackgroundService
    {
        private static readonly TimeSpan CheckPeriod = TimeSpan.FromSeconds(30);

        /// <summary>The most transactions named in one alert, so a long list cannot flood the log.</summary>
        private const int MaxNamedTransactions = 10;

        private readonly ThreadHeartbeats _heartbeats;
        private readonly ILogger<ThreadWatchdogService> _logger;
        private readonly TimeProvider _time;
        private readonly TimeSpan _staleAfter;
        private readonly InFlightTransactions? _transactions;
        private readonly HashSet<string> _alerted = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates the watchdog. <paramref name="transactions"/> is optional: a host without the data layer has no transactions to
        /// report, and the alert says so instead of implying none were open.
        /// </summary>
        public ThreadWatchdogService(
            ThreadHeartbeats heartbeats,
            ILogger<ThreadWatchdogService> logger,
            TimeProvider time,
            TimeSpan staleAfter,
            InFlightTransactions? transactions = null)
        {
            _heartbeats = heartbeats ?? throw new ArgumentNullException(nameof(heartbeats));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _staleAfter = staleAfter;
            _transactions = transactions;
        }

        /// <summary>Runs one check. Public so tests can drive it without waiting for the timer.</summary>
        public void Check()
        {
            DateTimeOffset now = _time.GetUtcNow();
            IReadOnlyList<string> stale = _heartbeats.StaleServices(now, _staleAfter);

            foreach (string name in stale)
            {
                if (_alerted.Add(name))
                {
                    // Logged as a message only, never with the exception or a stack trace. The open transactions say what the
                    // stalled work was waiting on, which is the part an operator needs.
                    string inFlight = _transactions is null
                        ? "transaction state is not tracked"
                        : DescribeInFlight(_transactions.Snapshot(), now);
                    _logger.LogError("Background loop {ServiceName} has not ticked for more than {StaleAfter}; it may be hung. {InFlight}",
                        name, _staleAfter, inFlight);
                }
            }

            // A loop that is no longer stale can alert again the next time it goes stale.
            _alerted.RemoveWhere(name => !stale.Contains(name, StringComparer.Ordinal));
        }

        /// <summary>
        /// Describes the open transactions for an alert: each caller and how long it has been open, oldest first. Public static
        /// so the wording can be tested without a logger.
        /// </summary>
        public static string DescribeInFlight(IReadOnlyList<InFlightTransactions.InFlightTransaction> open, DateTimeOffset now)
        {
            if (open.Count == 0)
            {
                return "no unit of work in flight";
            }

            var parts = open.Take(MaxNamedTransactions)
                .Select(t => $"{t.Caller} for {(long)(now - t.StartedUtc).TotalSeconds} s");
            string named = string.Join(", ", parts);
            int more = open.Count - MaxNamedTransactions;
            return more > 0 ? $"in flight: {named}, and {more} more" : $"in flight: {named}";
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(CheckPeriod, _time);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    Check();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }
    }
}
