using System.Reflection;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Services;

using ContractDevice = VideoForensics.Core.Providers.Common.Contracts.Device;
using ContractLocation = VideoForensics.Core.Providers.Common.Contracts.Location;
using DeviceDetailFields = VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields;
using StoredDevice = VideoForensics.Data.Common.Entities.Device;
using StoredLocation = VideoForensics.Data.Common.Entities.Location;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>Discovery reads come from stored rows only and map back to the provider contract types.</summary>
    public class StoredDeviceDiscoveryReaderTests
    {
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();

        private StoredDeviceDiscoveryReader CreateReader() => new(_devices.Object, _locations.Object);

        private static StoredLocation Loc(string providerId, string name, string? address = null) =>
            new() { Id = Guid.NewGuid(), ProviderLocationId = providerId, Name = name, Address = address };

        private static StoredDevice Dev(StoredLocation loc, string providerId, string name = "Cam") =>
            new() { Id = Guid.NewGuid(), LocationId = loc.Id, ProviderDeviceId = providerId, Name = name, Type = "doorbell", IsOnline = true };

        [Fact]
        public async Task StoredDeviceDiscoveryReader_GetLocations_ReturnsStoredLocationsWithProviderIds()
        {
            StoredLocation home = Loc("prov-home", "Home", "1 Main St");
            _ = _locations.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([home]);

            IReadOnlyList<ContractLocation> result = await CreateReader().GetLocationsAsync(CancellationToken.None);

            ContractLocation only = Assert.Single(result);
            Assert.Equal("prov-home", only.Id);
            Assert.Equal("Home", only.Name);
            Assert.Equal("1 Main St", only.Address);
        }

        [Fact]
        public async Task StoredDeviceDiscoveryReader_GetDevicesByLocation_ReturnsOnlyThatLocationsDevices()
        {
            StoredLocation home = Loc("prov-home", "Home");
            StoredDevice door = Dev(home, "dev-1", "Front Door");
            _ = _locations.Setup(x => x.GetByProviderLocationIdAsync("prov-home", It.IsAny<CancellationToken>())).ReturnsAsync(home);
            _ = _devices.Setup(x => x.GetByLocationIdAsync(home.Id, It.IsAny<CancellationToken>())).ReturnsAsync([door]);

            IReadOnlyList<ContractDevice> result = await CreateReader().GetDevicesAsync("prov-home", CancellationToken.None);

            ContractDevice only = Assert.Single(result);
            Assert.Equal("dev-1", only.Id);
            Assert.Equal("prov-home", only.LocationId);
            Assert.Equal("Front Door", only.Name);
            Assert.Equal("doorbell", only.Type);
            Assert.True(only.IsOnline);
            _devices.Verify(x => x.GetByLocationIdAsync(It.Is<Guid>(g => g != home.Id), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StoredDeviceDiscoveryReader_GetDevice_UnknownId_ReturnsNull()
        {
            StoredLocation home = Loc("prov-home", "Home");
            _ = _devices.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([Dev(home, "dev-1")]);

            ContractDevice? result = await CreateReader().GetDeviceAsync("missing", CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task StoredDeviceDiscoveryReader_DetailFieldsRoundTrip_MatchesWhatWasStored()
        {
            StoredLocation home = Loc("prov-home", "Home");
            StoredDevice row = Dev(home, "dev-1");
            DeviceDetailFields stored = FullyPopulatedDetails();
            // Mirror VideoForensicsDataClient.ApplyDetailFields: each scalar detail property lands on the same-named column.
            foreach (PropertyInfo p in ScalarDetailProperties())
            {
                typeof(StoredDevice).GetProperty(p.Name)!.SetValue(row, p.GetValue(stored));
            }

            _ = _devices.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([row]);
            _ = _locations.Setup(x => x.GetAsync(home.Id, It.IsAny<CancellationToken>())).ReturnsAsync(home);

            ContractDevice? result = await CreateReader().GetDeviceAsync("dev-1", CancellationToken.None);

            Assert.NotNull(result?.Details);
            Assert.Equal("prov-home", result.LocationId);
            foreach (PropertyInfo p in ScalarDetailProperties())
            {
                Assert.True(p.GetValue(stored) is not null, $"{p.Name} was not populated");
                Assert.Equal(p.GetValue(stored), p.GetValue(result.Details));
            }
        }

        private static IEnumerable<PropertyInfo> ScalarDetailProperties() =>
            typeof(DeviceDetailFields).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "EqualityContract" && !p.PropertyType.IsGenericType || Nullable.GetUnderlyingType(p.PropertyType) != null)
                .Where(p => p.Name != "EqualityContract");

        private static DeviceDetailFields FullyPopulatedDetails()
        {
            object?[] args = typeof(DeviceDetailFields).GetConstructors()[0].GetParameters().Select(p =>
            {
                Type t = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
                int seed = p.Position + 1;
                if (t == typeof(string)) { return (object?)$"v{seed}"; }
                if (t == typeof(bool)) { return seed % 2 == 0; }
                if (t == typeof(int)) { return seed; }
                if (t == typeof(double)) { return seed + 0.5; }
                if (t == typeof(decimal)) { return seed + 0.25m; }
                return null; // child collections live in child tables, not on the device row
            }).ToArray();
            return (DeviceDetailFields)typeof(DeviceDetailFields).GetConstructors()[0].Invoke(args);
        }
    }
}
