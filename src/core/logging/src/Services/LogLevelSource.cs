using Microsoft.Extensions.Logging;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>
    /// The minimum level the in-memory log buffer records. The host updates it when the saved logging level changes,
    /// so the live log view follows the same level as the console and the audit files.
    /// </summary>
    public sealed class LogLevelSource
    {
        private volatile int _minimum = (int)LogLevel.Trace;

        /// <summary>Entries below this level are not captured.</summary>
        public LogLevel Minimum
        {
            get => (LogLevel)_minimum;
            set => _minimum = (int)value;
        }
    }
}
