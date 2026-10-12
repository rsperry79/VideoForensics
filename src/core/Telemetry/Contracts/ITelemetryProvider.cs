using System;
using System.Collections.Generic;

namespace VideoForensics.Core.Telemetry.Contracts
{
    /// <summary>
    /// Provider-agnostic telemetry surface for service code. Callers emit spans, metrics and events
    /// through this interface only - never against a vendor SDK (OpenTelemetry, Azure Monitor, Jaeger,
    /// Prometheus, etc.) directly - so the concrete exporter can be swapped entirely in DI composition
    /// (see <see cref="VideoForensics.Core.Telemetry.DependencyInjection.ServiceCollectionExtensions"/>)
    /// without touching a single call site.
    /// </summary>
    public interface ITelemetryProvider
    {
        /// <summary>
        /// Starts a new telemetry span (distributed-tracing activity) and returns a scope that ends the
        /// span when disposed. Nest calls (e.g. in a using block) to build a trace; child spans are
        /// automatically parented to whichever span is active on the current async flow.
        /// </summary>
        /// <param name="name">The span's operation name.</param>
        /// <param name="tags">Optional key/value attributes attached to the span.</param>
        /// <returns>A disposable that ends the span when disposed. Never null in practice, but callers should still dispose it in a using block.</returns>
        IDisposable? StartSpan(string name, IReadOnlyDictionary<string, object?>? tags = null);

        /// <summary>Records a point-in-time numeric measurement (e.g. a duration, queue depth, byte count) against the named metric.</summary>
        /// <param name="name">The metric's name.</param>
        /// <param name="value">The measured value.</param>
        /// <param name="tags">Optional key/value dimensions attached to the measurement.</param>
        void RecordMetric(string name, double value, IReadOnlyDictionary<string, object?>? tags = null);

        /// <summary>Records a discrete, named occurrence (a trace point) - attached to the current span when one is active.</summary>
        /// <param name="name">The event's name.</param>
        /// <param name="properties">Optional key/value properties describing the event.</param>
        void RecordEvent(string name, IReadOnlyDictionary<string, object?>? properties = null);
    }
}
