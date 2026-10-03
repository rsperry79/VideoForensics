using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;

using System;
using System.Runtime.Versioning;

using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Core.Logging.Providers;
using VideoForensics.Core.Logging.Services;

namespace VideoForensics.Core.Logging.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>Adds the action logger service to the dependency injection container.</summary>
        public static IServiceCollection AddActionLogger(this IServiceCollection services)
        {
            _ = services.AddScoped<IActionLogger, ActionLogger>();
            return services;
        }

        /// <summary>
        /// Registers Serilog-based structured logging with rolling file sinks (text and JSON formats).
        /// When Serilog is configured via Log.Logger before host creation, this method integrates it
        /// with the logging pipeline. Optionally enables Windows Event Log and/or Linux syslog for
        /// unattended-service visibility.
        ///
        /// NOTE: For Serilog configuration, configure Log.Logger BEFORE calling this method.
        /// This method assumes Serilog's static Log.Logger is already set up with file sinks.
        /// </summary>
        public static ILoggingBuilder AddVideoForensicsLogging(
            this ILoggingBuilder logging,
            string logFilePath,
            LogLevel minimumLevel = LogLevel.Information,
            bool enableEventLog = false,
            bool enableSyslog = false,
            bool enableNamedPipeLogger = false)
        {
            // Serilog integration: if Serilog is configured globally via Log.Logger, wire it into the logging pipeline.
            // The logFilePath parameter is kept for backward compatibility but is now superseded by
            // Serilog's configuration (which should be done before host creation in Program.cs).
            if (Serilog.Log.Logger != null)
            {
                _ = logging.AddSerilog(dispose: true);
            }
            else
            {
                // Fallback for hosts that don't pre-configure Serilog: use legacy file provider.
                _ = logging.AddProvider(new FileLoggerProvider(logFilePath, minimumLevel));
            }

            if (enableEventLog && OperatingSystem.IsWindows())
            {
                AddWindowsEventLog(logging);
            }

            if (enableSyslog && OperatingSystem.IsLinux())
            {
                // Additional syslog configuration (independent of file sinks).
                Serilog.Core.Logger syslogLogger = new LoggerConfiguration()
                    .WriteTo.LocalSyslog(appName: "VideoForensics")
                    .CreateLogger();
                _ = logging.AddSerilog(syslogLogger, dispose: true);
            }

            if (enableNamedPipeLogger && OperatingSystem.IsWindows())
            {
                _ = logging.AddProvider(new NamedPipeLoggerProvider());
            }

            return logging;
        }

        /// <summary>
        /// Adds the NamedPipeLoggerProvider for Logger Viewer client consumption.
        /// Windows-only; no-op on other platforms.
        /// </summary>
        public static ILoggingBuilder AddNamedPipeLogger(this ILoggingBuilder logging)
        {
            if (OperatingSystem.IsWindows())
            {
                _ = logging.AddProvider(new NamedPipeLoggerProvider());
            }

            return logging;
        }

        [SupportedOSPlatform("windows")]
        private static void AddWindowsEventLog(ILoggingBuilder logging)
        {
            _ = logging.AddEventLog(settings => settings.SourceName = "VideoForensics");
        }
    }
}
