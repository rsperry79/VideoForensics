using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Hosting.BackgroundServices;

namespace VideoForensics.Hosting.Tests
{
    public class SnapshotCaptureRegistrationTests
    {
        private static IConfiguration Config(params (string Key, string Value)[] values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
                .Build();
        }

        [Fact]
        public void AddVideoForensicsSnapshotCapture_NoSwitch_RegistersHostedServiceByDefault()
        {
            var services = new ServiceCollection();

            _ = services.AddVideoForensicsSnapshotCapture(Config());

            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(SnapshotCaptureService));
            Assert.Contains(services, d => d.ServiceType == typeof(IProviderSnapshotCapture) && d.IsKeyedService && Equals(d.ServiceKey, "Ring"));
        }

        [Fact]
        public void AddVideoForensicsSnapshotCapture_EnabledFalse_RegistersNothing()
        {
            var services = new ServiceCollection();

            _ = services.AddVideoForensicsSnapshotCapture(Config(("SnapshotCapture:Enabled", "false")));

            Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(SnapshotCaptureService));
            Assert.DoesNotContain(services, d => d.ServiceType == typeof(IProviderSnapshotCapture));
        }
    }
}
