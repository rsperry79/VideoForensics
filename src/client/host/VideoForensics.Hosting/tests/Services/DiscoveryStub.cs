namespace VideoForensics.Hosting.Tests.Services
{
    using VideoForensics.Core.Providers.Common.Contracts;
    using LocationEntity = VideoForensics.Data.Common.Entities.Location;
    using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

    /// <summary>
    /// Makes provider discovery return one location and its devices, and makes the repositories return the same stored
    /// entities, so a pull updates those instances. Lets the pull tests describe an account by its stored inventory.
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
            discovery.Setup(d => d.GetLocationsAsync(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Location> { new(location.ProviderLocationId, location.Name) });

            discovery.Setup(d => d.GetDevicesAsync(accountId, location.ProviderLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices.Select(d => new Device(d.ProviderDeviceId, d.Name, d.Type, location.ProviderLocationId, d.IsOnline)).ToList());

            locations.Setup(r => r.GetByProviderLocationIdAsync(accountId, location.ProviderLocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(location);

            foreach (DeviceEntity device in devices)
            {
                deviceRepository.Setup(r => r.GetByProviderDeviceIdAsync(location.Id, device.ProviderDeviceId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(device);
            }
        }
    }
}
