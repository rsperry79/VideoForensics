using System;

using Serilog;
using Serilog.Configuration;

using VideoForensics.Core.Logging.Serilog.Sinks;

namespace VideoForensics.Core.Logging.Serilog.Extensions
{
    /// <summary>
    /// Serilog configuration extensions for Unix socket sink.
    /// </summary>
    public static class UnixSocketLoggerConfigurationExtensions
    {
        /// <summary>
        /// Adds a Unix socket sink to the Serilog logger configuration.
        /// Linux-only; no-op on other platforms.
        /// </summary>
        /// <param name="sinkConfiguration">The sink configuration.</param>
        /// <returns>The logger configuration.</returns>
        public static LoggerConfiguration UnixSocket(
            this LoggerSinkConfiguration sinkConfiguration)
        {
            if (!OperatingSystem.IsLinux())
            {
                // Return the configuration unchanged on non-Linux platforms
                return sinkConfiguration.Logger(c => c.MinimumLevel.Fatal());
            }

            var sink = new UnixSocketSink();
            return sinkConfiguration.Sink(sink);
        }
    }
}
