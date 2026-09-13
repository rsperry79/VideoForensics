using System;
using System.Runtime.Versioning;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Extensions.Logging;

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
        /// Registers the shared file logger, and optionally Windows Event Log and/or Linux syslog, for
        /// hosts that need durable logging without (or in addition to) console output. WebApp enables
        /// Event Log (Windows) / syslog (Linux) for unattended-service visibility; MAUI and the legacy
        /// console app only need the file provider (their own UI - Blazor MAUI, Spectre.Console - is
        /// why they can't rely on console logging).
        /// </summary>
        public static ILoggingBuilder AddVideoForensicsLogging(
            this ILoggingBuilder logging,
            string logFilePath,
            LogLevel minimumLevel = LogLevel.Information,
            bool enableEventLog = false,
            bool enableSyslog = false)
        {
            logging.AddProvider(new FileLoggerProvider(logFilePath, minimumLevel));

            if (enableEventLog && OperatingSystem.IsWindows())
            {
                AddWindowsEventLog(logging);
            }

            if (enableSyslog && OperatingSystem.IsLinux())
            {
                Serilog.Core.Logger syslogLogger = new LoggerConfiguration()
                    .WriteTo.LocalSyslog(appName: "VideoForensics")
                    .CreateLogger();
                logging.AddSerilog(syslogLogger, dispose: true);
            }

            return logging;
        }

        [SupportedOSPlatform("windows")]
        private static void AddWindowsEventLog(ILoggingBuilder logging) =>
            logging.AddEventLog(settings => settings.SourceName = "VideoForensics");
    }
}
