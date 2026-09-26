using System;
using System.Collections.Generic;

using VideoForensics.Core.Telemetry.Contracts;
using VideoForensics.Core.Telemetry.Services;

using Xunit;

namespace VideoForensics.Core.Telemetry.Tests
{
    public class NullTelemetryProviderTests
    {
        private readonly NullTelemetryProvider _provider = new();

        [Fact]
        public void StartSpan_WithNameOnly_ReturnsNonNullDisposable()
        {
            IDisposable? scope = _provider.StartSpan("test-span");

            Assert.NotNull(scope);
        }

        [Fact]
        public void StartSpan_DisposingReturnedScope_DoesNotThrow()
        {
            IDisposable? scope = _provider.StartSpan("test-span", new Dictionary<string, object?> { ["key"] = "value" });

            Exception? exception = Record.Exception(() => scope!.Dispose());

            Assert.Null(exception);
        }

        [Fact]
        public void RecordMetric_WithTags_DoesNotThrow()
        {
            Exception? exception = Record.Exception(() =>
                _provider.RecordMetric("test-metric", 42.0, new Dictionary<string, object?> { ["key"] = "value" }));

            Assert.Null(exception);
        }

        [Fact]
        public void RecordMetric_WithoutTags_DoesNotThrow()
        {
            Exception? exception = Record.Exception(() => _provider.RecordMetric("test-metric", 1.0));

            Assert.Null(exception);
        }

        [Fact]
        public void RecordEvent_WithProperties_DoesNotThrow()
        {
            Exception? exception = Record.Exception(() =>
                _provider.RecordEvent("test-event", new Dictionary<string, object?> { ["key"] = "value" }));

            Assert.Null(exception);
        }

        [Fact]
        public void RecordEvent_WithoutProperties_DoesNotThrow()
        {
            Exception? exception = Record.Exception(() => _provider.RecordEvent("test-event"));

            Assert.Null(exception);
        }

        [Fact]
        public void NullTelemetryProvider_ImplementsITelemetryProvider()
        {
            Assert.IsAssignableFrom<ITelemetryProvider>(_provider);
        }
    }
}
