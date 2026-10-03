using System;

using Serilog;
using Serilog.Configuration;

using VideoForensics.Core.Logging.Serilog.Sinks;

namespace VideoForensics.Core.Logging.Serilog.Extensions
{
    /// <summary>
    /// Serilog configuration extensions for named pipe sink.
    /// </summary>
    public static class NamedPipeLoggerConfigurationExtensions
    {
        /// <summary>
        /// Adds a named pipe sink to the Serilog logger configuration.
        /// Windows-only; no-op on other platforms.
        /// </summary>
        /// <param name="sinkConfiguration">The sink configuration.</param>
        /// <returns>The logger configuration.</returns>
        public static LoggerConfiguration NamedPipe(
            this LoggerSinkConfiguration sinkConfiguration)
        {
            if (!OperatingSystem.IsWindows())
            {
                // Return the configuration unchanged on non-Windows platforms
                return sinkConfiguration.Logger(c => c.MinimumLevel.Fatal());
            }

            var sink = new NamedPipeSink();
            return sinkConfiguration.Sink(sink);
        }
    }
}
