using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Core.Services
{
    /// <summary>
    /// Holds the last configuration read from the database for the whole process. Register as a singleton: the config service is
    /// scoped, and the snapshot, sync and path services each load it several times per tick.
    /// </summary>
    public sealed class ConfigurationCache
    {
        private readonly object _gate = new();
        private ForensicsConfiguration? _config;

        public ForensicsConfiguration? Get()
        {
            lock (_gate)
            {
                return _config;
            }
        }

        public void Set(ForensicsConfiguration config)
        {
            lock (_gate)
            {
                _config = config;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _config = null;
            }
        }
    }
}
