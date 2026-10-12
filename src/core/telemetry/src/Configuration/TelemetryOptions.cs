namespace VideoForensics.Core.Telemetry.Configuration
{
    /// <summary>
    /// Composition-time configuration for <see cref="VideoForensics.Core.Telemetry.DependencyInjection.ServiceCollectionExtensions.AddVideoForensicsTelemetry"/>.
    /// Exactly one exporter destination is picked at startup - Azure Monitor when
    /// <see cref="AzureMonitorConnectionString"/> is set, otherwise a generic OTLP endpoint (Jaeger,
    /// SigNoz, an OpenTelemetry Collector, etc.) when <see cref="OtlpEndpoint"/> is set, otherwise the
    /// OpenTelemetry SDK still runs locally with no exporter wired up.
    /// </summary>
    public sealed class TelemetryOptions
    {
        /// <summary>When false, telemetry collection is skipped entirely and a no-op provider is registered.</summary>
        public bool Enabled { get; set; }

        /// <summary>Application Insights connection string. When set (and <see cref="Enabled"/> is true), traces and metrics are exported to Azure Monitor.</summary>
        public string? AzureMonitorConnectionString { get; set; }

        /// <summary>
        /// Generic OTLP collector endpoint (e.g. a local Jaeger, SigNoz or OpenTelemetry Collector
        /// instance). Used only when <see cref="AzureMonitorConnectionString"/> is not set.
        /// </summary>
        public string? OtlpEndpoint { get; set; }
    }
}
