using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using VideoForensics.Core.Telemetry.Contracts;

namespace VideoForensics.Core.Telemetry.Services
{
    /// <summary>
    /// <see cref="ITelemetryProvider"/> built on the vendor-neutral OpenTelemetry SDK primitives
    /// (<see cref="ActivitySource"/> for spans, <see cref="Meter"/> for metrics). This class never talks
    /// to Azure Monitor, Jaeger, or any other backend directly - that wiring lives entirely in
    /// <see cref="DependencyInjection.ServiceCollectionExtensions.AddVideoForensicsTelemetry"/>, which
    /// registers whichever exporter is configured to listen on <see cref="SourceName"/>/<see cref="MeterName"/>.
    /// </summary>
    public sealed class OpenTelemetryProvider : ITelemetryProvider, IDisposable
    {
        /// <summary>Well-known <see cref="ActivitySource"/> name that DI wiring registers with AddSource so exporters actually pick up spans started here.</summary>
        public const string SourceName = "VideoForensics";

        /// <summary>Well-known <see cref="Meter"/> name that DI wiring registers with AddMeter so exporters actually pick up metrics recorded here.</summary>
        public const string MeterName = "VideoForensics";

        private static readonly ActivitySource ActivitySource = new(SourceName);

        private readonly Meter _meter;
        private readonly Histogram<double> _metricHistogram;

        public OpenTelemetryProvider()
        {
            _meter = new Meter(MeterName);
            // A histogram (not a monotonic Counter<T>) because RecordMetric's values are arbitrary
            // point-in-time measurements (durations, gauges, etc.), not always-increasing counts.
            _metricHistogram = _meter.CreateHistogram<double>("videoforensics.metric");
        }

        /// <inheritdoc />
        public IDisposable? StartSpan(string name, IReadOnlyDictionary<string, object?>? tags = null)
        {
            Activity? activity = ActivitySource.StartActivity(name);

            if (activity is not null && tags is not null)
            {
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    _ = activity.SetTag(tag.Key, tag.Value);
                }
            }

            return activity;
        }

        /// <inheritdoc />
        public void RecordMetric(string name, double value, IReadOnlyDictionary<string, object?>? tags = null)
        {
            if (tags is null || tags.Count == 0)
            {
                _metricHistogram.Record(value, new KeyValuePair<string, object?>("metric.name", name));
                return;
            }

            var tagList = new TagList
            {
                { "metric.name", name }
            };

            foreach (KeyValuePair<string, object?> tag in tags)
            {
                tagList.Add(tag.Key, tag.Value);
            }

            _metricHistogram.Record(value, tagList);
        }

        /// <inheritdoc />
        public void RecordEvent(string name, IReadOnlyDictionary<string, object?>? properties = null)
        {
            Activity? current = Activity.Current;

            if (current is null)
            {
                return;
            }

            if (properties is null || properties.Count == 0)
            {
                _ = current.AddEvent(new ActivityEvent(name));
                return;
            }

            var tags = new ActivityTagsCollection();
            foreach (KeyValuePair<string, object?> property in properties)
            {
                tags.Add(property.Key, property.Value);
            }

            _ = current.AddEvent(new ActivityEvent(name, tags: tags));
        }

        /// <summary>Disposes the underlying <see cref="Meter"/>. The shared <see cref="ActivitySource"/> is intentionally not disposed - it is a process-wide, statically held source.</summary>
        public void Dispose()
        {
            _meter.Dispose();
        }
    }
}
