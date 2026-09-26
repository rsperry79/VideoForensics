using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Core.Telemetry.Configuration;
using VideoForensics.Core.Telemetry.Contracts;
using VideoForensics.Core.Telemetry.DependencyInjection;
using VideoForensics.Core.Telemetry.Services;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    /// <summary>
    /// Exercises the telemetry wiring added to <see cref="VideoForensicsHostingExtensions.AddVideoForensicsServerCore"/>.
    /// Building the full <c>AddVideoForensicsServerCore</c> DI graph in a unit test would require a real
    /// database/configuration for its many other registrations, so these tests instead exercise
    /// <see cref="ServiceCollectionExtensions.AddVideoForensicsTelemetry"/> directly - the exact call
    /// <c>AddVideoForensicsServerCore</c> now makes with <c>telemetryOptions ?? new TelemetryOptions()</c> -
    /// against a minimal <see cref="IServiceCollection"/>, proving the wiring resolves the correct
    /// <see cref="ITelemetryProvider"/> implementation.
    /// </summary>
    public class VideoForensicsHostingExtensionsTelemetryTests
    {
        [Fact]
        public void AddVideoForensicsTelemetry_WithDisabledOptions_ResolvesNullTelemetryProvider()
        {
            // Arrange - mirrors AddVideoForensicsServerCore's default when telemetryOptions is null
            // (telemetryOptions ?? new TelemetryOptions()), i.e. telemetry is off unless explicitly enabled.
            var services = new ServiceCollection();
            var options = new TelemetryOptions { Enabled = false };

            // Act
            _ = services.AddVideoForensicsTelemetry(options);
            using ServiceProvider provider = services.BuildServiceProvider();
            ITelemetryProvider telemetryProvider = provider.GetRequiredService<ITelemetryProvider>();

            // Assert
            Assert.IsType<NullTelemetryProvider>(telemetryProvider);
        }

        [Fact]
        public void AddVideoForensicsServerCore_DefaultTelemetryOptions_IsDisabledByDefault()
        {
            // Arrange / Act - the same fallback AddVideoForensicsServerCore applies when its optional
            // telemetryOptions parameter is left unset, so existing callers see no behavior change.
            TelemetryOptions? telemetryOptions = null;
            TelemetryOptions effectiveOptions = telemetryOptions ?? new TelemetryOptions();

            // Assert
            Assert.False(effectiveOptions.Enabled);
        }

        [Fact]
        public void AddVideoForensicsTelemetry_WithEnabledOptions_ResolvesOpenTelemetryProvider()
        {
            // Arrange
            var services = new ServiceCollection();
            var options = new TelemetryOptions { Enabled = true };

            // Act
            _ = services.AddVideoForensicsTelemetry(options);
            using ServiceProvider provider = services.BuildServiceProvider();
            ITelemetryProvider telemetryProvider = provider.GetRequiredService<ITelemetryProvider>();

            // Assert
            Assert.IsType<OpenTelemetryProvider>(telemetryProvider);
        }
    }
}
