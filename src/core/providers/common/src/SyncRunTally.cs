namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Default <see cref="ISyncRunTally"/>: plain counters. Not thread-safe; a sync run is sequential within one DI scope.
    /// </summary>
    public sealed class SyncRunTally : ISyncRunTally
    {
        private bool _configRan;
        private int _locations;
        private int _devices;
        private int _configAreas;
        private int _eventsPulled;
        private int _eventsAlreadyPresent;
        private int _eventsNotPulled;
        private int _feedsFailed;

        /// <inheritdoc />
        public void Reset()
        {
            _configRan = false;
            _locations = 0;
            _devices = 0;
            _configAreas = 0;
            _eventsPulled = 0;
            _eventsAlreadyPresent = 0;
            _eventsNotPulled = 0;
            _feedsFailed = 0;
        }

        /// <inheritdoc />
        public void MarkConfigRan() => _configRan = true;

        /// <inheritdoc />
        public void AddConfigArea() => _configAreas++;

        /// <inheritdoc />
        public void AddEventsPulled(int count) => _eventsPulled += count;

        /// <inheritdoc />
        public void AddEventsAlreadyPresent(int count) => _eventsAlreadyPresent += count;

        /// <inheritdoc />
        public void AddEventsNotPulled(int count) => _eventsNotPulled += count;

        /// <inheritdoc />
        public void AddFeedFailure() => _feedsFailed++;

        /// <inheritdoc />
        public void SetInventory(int locationCount, int deviceCount)
        {
            _locations = locationCount;
            _devices = deviceCount;
        }

        /// <inheritdoc />
        public SyncRunTotals Snapshot() =>
            new(_configRan, _locations, _devices, _configAreas, _eventsPulled + _eventsAlreadyPresent, _feedsFailed, _eventsNotPulled, _eventsAlreadyPresent);
    }
}
