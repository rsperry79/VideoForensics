using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Core.Telemetry.Configuration;
using VideoForensics.Core.Telemetry.Contracts;
using VideoForensics.Core.Telemetry.DependencyInjection;
using VideoForensics.Core.Telemetry.Services;

using Xunit;

namespace VideoForensics.Core.Telemetry.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddVideoForensicsTelemetry_WhenDisabled_ResolvesNullTelemetryProvider()
        {
            // Arrange
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
        public void AddVideoForensicsTelemetry_WhenEnabled_ResolvesOpenTelemetryProvider()
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

        [Fact]
        public void AddVideoForensicsTelemetry_WhenEnabled_RegistersSingletonInstance()
        {
            // Arrange
            var services = new ServiceCollection();
            var options = new TelemetryOptions { Enabled = true };

            // Act
            _ = services.AddVideoForensicsTelemetry(options);
            using ServiceProvider provider = services.BuildServiceProvider();
            ITelemetryProvider first = provider.GetRequiredService<ITelemetryProvider>();
            ITelemetryProvider second = provider.GetRequiredService<ITelemetryProvider>();

            // Assert
            Assert.Same(first, second);
        }

        [Fact]
        public void AddVideoForensicsTelemetry_ReturnsSameServiceCollectionForChaining()
        {
            var services = new ServiceCollection();
            var options = new TelemetryOptions { Enabled = false };

            IServiceCollection result = services.AddVideoForensicsTelemetry(options);

            Assert.Same(services, result);
        }
    }
}
