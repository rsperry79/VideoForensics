using System;
using System.Collections.Generic;

using VideoForensics.Core.Telemetry.Contracts;

namespace VideoForensics.Core.Telemetry.Services
{
    /// <summary>
    /// No-op <see cref="ITelemetryProvider"/> for tests and for hosts that run with telemetry disabled
    /// (see <see cref="Configuration.TelemetryOptions.Enabled"/>). Every member is a deliberate no-op
    /// so callers never need to branch on whether telemetry is turned on.
    /// </summary>
    public sealed class NullTelemetryProvider : ITelemetryProvider
    {
        /// <inheritdoc />
        public IDisposable? StartSpan(string name, IReadOnlyDictionary<string, object?>? tags = null)
        {
            return NoopScope.Instance;
        }

        /// <inheritdoc />
        public void RecordMetric(string name, double value, IReadOnlyDictionary<string, object?>? tags = null)
        {
        }

        /// <inheritdoc />
        public void RecordEvent(string name, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
