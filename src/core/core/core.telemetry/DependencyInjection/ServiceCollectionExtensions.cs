using Azure.Monitor.OpenTelemetry.Exporter;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using System;

using VideoForensics.Core.Telemetry.Configuration;
using VideoForensics.Core.Telemetry.Contracts;
using VideoForensics.Core.Telemetry.Services;

namespace VideoForensics.Core.Telemetry.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the provider-agnostic <see cref="ITelemetryProvider"/> abstraction. When
        /// <paramref name="options"/>.Enabled is false, a no-op provider is registered and the
        /// OpenTelemetry SDK is not wired up at all. Otherwise <see cref="OpenTelemetryProvider"/> is
        /// registered and the OpenTelemetry SDK is configured to listen on the shared
        /// "VideoForensics" ActivitySource/Meter, exporting to Azure Monitor when
        /// <see cref="TelemetryOptions.AzureMonitorConnectionString"/> is set, to a generic OTLP
        /// endpoint (Jaeger, SigNoz, an OpenTelemetry Collector, etc.) when
        /// <see cref="TelemetryOptions.OtlpEndpoint"/> is set instead, or with no exporter at all
        /// (useful for local dev) when neither is set. Swapping exporters is purely a matter of
        /// changing these options - no caller of <see cref="ITelemetryProvider"/> ever changes.
        /// </summary>
        public static IServiceCollection AddVideoForensicsTelemetry(this IServiceCollection services, TelemetryOptions options)
        {
            if (!options.Enabled)
            {
                _ = services.AddSingleton<ITelemetryProvider, NullTelemetryProvider>();
                return services;
            }

            _ = services.AddSingleton<ITelemetryProvider, OpenTelemetryProvider>();

            _ = services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    _ = tracing.AddSource(OpenTelemetryProvider.SourceName);
                    AddConfiguredExporter(tracing, options);
                })
                .WithMetrics(metrics =>
                {
                    _ = metrics.AddMeter(OpenTelemetryProvider.MeterName);
                    AddConfiguredExporter(metrics, options);
                });

            return services;
        }

        private static void AddConfiguredExporter(TracerProviderBuilder tracing, TelemetryOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.AzureMonitorConnectionString))
            {
                string connectionString = options.AzureMonitorConnectionString;
                _ = tracing.AddAzureMonitorTraceExporter(azureOptions => azureOptions.ConnectionString = connectionString);
            }
            else if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            {
                string endpoint = options.OtlpEndpoint;
                _ = tracing.AddOtlpExporter(otlpOptions => otlpOptions.Endpoint = new Uri(endpoint));
            }
        }

        private static void AddConfiguredExporter(MeterProviderBuilder metrics, TelemetryOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.AzureMonitorConnectionString))
            {
                string connectionString = options.AzureMonitorConnectionString;
                _ = metrics.AddAzureMonitorMetricExporter(azureOptions => azureOptions.ConnectionString = connectionString);
            }
            else if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            {
                string endpoint = options.OtlpEndpoint;
                _ = metrics.AddOtlpExporter(otlpOptions => otlpOptions.Endpoint = new Uri(endpoint));
            }
        }
    }
}
