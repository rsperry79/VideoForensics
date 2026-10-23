using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        private readonly ThreadHeartbeats _heartbeats;
        private readonly ILogger<ThreadWatchdogService> _logger;
        private readonly TimeProvider _time;
        private readonly TimeSpan _staleAfter;
        private readonly HashSet<string> _alerted = new(StringComparer.Ordinal);

        public ThreadWatchdogService(ThreadHeartbeats heartbeats, ILogger<ThreadWatchdogService> logger, TimeProvider time, TimeSpan staleAfter)
        {
            _heartbeats = heartbeats ?? throw new ArgumentNullException(nameof(heartbeats));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _staleAfter = staleAfter;
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
                    _logger.LogError("Background loop {ServiceName} has not ticked for more than {StaleAfter}; it may be hung", name, _staleAfter);
                }
            }

            // A loop that is no longer stale can alert again the next time it goes stale.
            _alerted.RemoveWhere(name => !stale.Contains(name, StringComparer.Ordinal));
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
