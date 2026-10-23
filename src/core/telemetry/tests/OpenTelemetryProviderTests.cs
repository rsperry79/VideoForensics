using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using VideoForensics.Core.Telemetry.Services;

using Xunit;

namespace VideoForensics.Core.Telemetry.Tests
{
    public class OpenTelemetryProviderTests
    {
        [Fact]
        public void StartSpan_WithName_StartsActivityWithMatchingName()
        {
            // Arrange
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == OpenTelemetryProvider.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => capturedActivity = activity
            };
            ActivitySource.AddActivityListener(listener);

            var provider = new OpenTelemetryProvider();

            // Act
            using IDisposable? scope = provider.StartSpan("my-span");

            // Assert
            Assert.NotNull(capturedActivity);
            Assert.Equal("my-span", capturedActivity!.OperationName);
        }

        [Fact]
        public void StartSpan_WithTags_SetsTagsOnActivity()
        {
            // Arrange
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == OpenTelemetryProvider.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => capturedActivity = activity
            };
            ActivitySource.AddActivityListener(listener);

            var provider = new OpenTelemetryProvider();
            var tags = new Dictionary<string, object?> { ["device.id"] = "abc-123" };

            // Act
            using IDisposable? scope = provider.StartSpan("tagged-span", tags);

            // Assert
            Assert.NotNull(capturedActivity);
            Assert.Equal("abc-123", capturedActivity!.GetTagItem("device.id"));
        }

        [Fact]
        public void RecordMetric_WithValue_PublishesMeasurementToMeter()
        {
            // Arrange
            double? recordedValue = null;
            using var meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == OpenTelemetryProvider.MeterName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => recordedValue = measurement);
            meterListener.Start();

            var provider = new OpenTelemetryProvider();

            // Act
            provider.RecordMetric("my-metric", 3.14);

            // Assert
            Assert.Equal(3.14, recordedValue);
        }

        [Fact]
        public void RecordEvent_WithActiveActivity_AddsEventToActivity()
        {
            // Arrange
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == OpenTelemetryProvider.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            var provider = new OpenTelemetryProvider();

            // Act
            using (IDisposable? scope = provider.StartSpan("event-span"))
            {
                provider.RecordEvent("something-happened", new Dictionary<string, object?> { ["reason"] = "test" });

                // Assert
                Activity? current = Activity.Current;
                Assert.NotNull(current);
                Assert.Contains(current!.Events, e => e.Name == "something-happened");
            }
        }

        [Fact]
        public void RecordEvent_WithoutActiveActivity_DoesNotThrow()
        {
            var provider = new OpenTelemetryProvider();

            Exception? exception = Record.Exception(() => provider.RecordEvent("no-span-event"));

            Assert.Null(exception);
        }
    }
}
