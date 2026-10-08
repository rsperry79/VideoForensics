using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        private const int DefaultInMemoryBufferCapacity = 5000;

        /// <summary>Adds the action logger service to the dependency injection container.</summary>
        public static IServiceCollection AddActionLogger(this IServiceCollection services)
        {
            _ = services.AddScoped<IActionLogger, ActionLogger>();
            return services;
        }

        /// <summary>
        /// Adds the in-memory log buffer to the dependency injection container as a singleton.
        /// The buffer is registered with the default capacity of 5000 entries.
        /// </summary>
        public static IServiceCollection AddInMemoryLogBuffer(this IServiceCollection services)
        {
            _ = services.AddSingleton(new InMemoryLogBuffer(capacity: DefaultInMemoryBufferCapacity));
            return services;
        }

        /// <summary>
        /// Adds the in-memory log buffer to the dependency injection container as a singleton with custom capacity.
        /// </summary>
        public static IServiceCollection AddInMemoryLogBuffer(this IServiceCollection services, int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

            _ = services.AddSingleton(new InMemoryLogBuffer(capacity: capacity));
            return services;
        }

        /// <summary>
        /// Registers Serilog-based structured logging with rolling file sinks (text and JSON formats).
        /// When Serilog is configured via Log.Logger before host creation, this method integrates it
        /// with the logging pipeline. Optionally enables Windows Event Log and/or Linux syslog for
        /// unattended-service visibility. Also registers the in-memory buffer provider.
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

            // Resolve the buffer lazily from the real container so registration order doesn't matter and the
            // provider writes to the same singleton the endpoints read (no throwaway service provider).
            logging.Services.AddSingleton<ILoggerProvider>(sp =>
                sp.GetService<InMemoryLogBuffer>() is { } buffer
                    ? new InMemoryLogBufferProvider(buffer)
                    : NullLoggerProvider.Instance);
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
