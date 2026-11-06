using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// Makes provider discovery return one location and its devices, and makes the repositories return the same stored
    /// entities, so a sync or pull updates those instances. Lets the pull tests describe an account by its stored inventory.
    /// The stored rows are also returned by the list and by-location reads a pull uses when no reference snapshot is cached.
    /// </summary>
    internal static class DiscoveryStub
    {
        public static void Wire(
            Guid accountId,
            LocationEntity location,
            IReadOnlyList<DeviceEntity> devices,
            Mock<IDeviceDiscoveryService> discovery,
            Mock<ILocationRepository> locations,
            Mock<IDeviceRepository> deviceRepository)
        {
            _ = discovery.Setup(d => d.GetLocationsAsync(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new(location.ProviderLocationId, location.Name)]);

            _ = discovery.Setup(d => d.GetDevicesAsync(accountId, location.ProviderLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices.Select(d => new Device(d.ProviderDeviceId, d.Name, d.Type, location.ProviderLocationId, d.IsOnline)).ToList());

            _ = locations.Setup(r => r.GetByProviderLocationIdAsync(accountId, location.ProviderLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(location);

            _ = locations.Setup(r => r.ListByProviderAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([location]);

            _ = deviceRepository.Setup(r => r.GetByLocationIdAsync(location.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices.ToList());

            foreach (DeviceEntity device in devices)
            {
                _ = deviceRepository.Setup(r => r.GetByProviderDeviceIdAsync(location.Id, device.ProviderDeviceId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(device);

                _ = deviceRepository.Setup(r => r.GetAsync(device.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(device);
            }
        }
    }
}
