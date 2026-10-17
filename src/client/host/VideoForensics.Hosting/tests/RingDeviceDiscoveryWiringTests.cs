using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Hosting.Tests
{
    /// <summary>
    /// Verifies that the Ring device discovery service built by the hosting DI graph receives the
    /// location and location-metadata repositories. Without them, PersistLocationMetadataAsync
    /// silently returns early in production.
    /// </summary>
    public class RingDeviceDiscoveryWiringTests
    {
        [Fact]
        public void BuildRingDeviceDiscoveryService_ResolvesLocationAndMetadataRepositories()
        {
            // Arrange - real data layer registrations (no database is opened during resolution),
            // with the Ring session provider stubbed since it is only needed for network calls.
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton(new Mock<ISessionProvider>().Object);
            _ = services.AddVideoForensicsDataLayer(Path.Combine(Path.GetTempPath(), "wiring-test.db"));
            _ = services.AddVideoForensicsMultiProviderServices(["Ring"]);

            using ServiceProvider provider = services.BuildServiceProvider();

            // Act
            IDeviceDiscoveryService discovery = provider.GetRequiredKeyedService<IDeviceDiscoveryService>("Ring");

            // Assert - the constructor keeps these as private fields; read them to prove they were passed.
            Assert.IsType<RingDeviceDiscoveryService>(discovery);
            object? locationRepository = GetPrivateField(discovery, "_locationRepository");
            object? metadataRepository = GetPrivateField(discovery, "_metadataRepository");
            Assert.NotNull(locationRepository);
            Assert.NotNull(metadataRepository);

            // Account settings (profile, ringtones, light groups, location mode) are persisted during sync.
            object? settingsPersister = GetPrivateField(discovery, "_settingsPersister");
            Assert.IsType<VideoForensics.Providers.Ring.Services.RingAccountSettingsPersister>(settingsPersister);
        }

        private static object? GetPrivateField(object instance, string name)
        {
            FieldInfo? field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field.GetValue(instance);
        }
    }
}
