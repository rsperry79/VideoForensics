using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests;

public class ReferenceDataCacheTests
{
    private static AccountReferenceData Snapshot(Guid locationId, string providerDeviceId)
    {
        var location = new CachedLocation(locationId, "location-1", "Home");
        var device = new CachedDevice(Guid.NewGuid(), locationId, "location-1", providerDeviceId, "Front Door");
        return new AccountReferenceData([location], new Dictionary<string, CachedDevice>(StringComparer.Ordinal) { [providerDeviceId] = device });
    }

    [Fact]
    public void Get_NothingReplaced_ReturnsNull()
    {
        var cache = new ReferenceDataCache();

        Assert.Null(cache.Get(Guid.NewGuid()));
    }

    [Fact]
    public void FindDevice_AfterReplace_ReturnsLinkedDevice()
    {
        var cache = new ReferenceDataCache();
        var accountId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        cache.Replace(accountId, Snapshot(locationId, "device-1"));

        CachedDevice? device = cache.FindDevice(accountId, "device-1");
        Assert.NotNull(device);
        Assert.Equal(locationId, device.LocationId);
        Assert.Equal("location-1", device.ProviderLocationId);
    }

    [Fact]
    public void FindDevice_OtherAccount_ReturnsNull()
    {
        var cache = new ReferenceDataCache();
        cache.Replace(Guid.NewGuid(), Snapshot(Guid.NewGuid(), "device-1"));

        Assert.Null(cache.FindDevice(Guid.NewGuid(), "device-1"));
    }

    [Fact]
    public void UpsertDevice_WithoutSnapshot_IsIgnored()
    {
        var cache = new ReferenceDataCache();
        var accountId = Guid.NewGuid();

        cache.UpsertDevice(accountId, new CachedDevice(Guid.NewGuid(), Guid.NewGuid(), "location-1", "device-1", null));

        Assert.Null(cache.Get(accountId));
    }

    [Fact]
    public void UpsertDevice_AddsDeviceAndKeepsLocations()
    {
        var cache = new ReferenceDataCache();
        var accountId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        cache.Replace(accountId, Snapshot(locationId, "device-1"));

        cache.UpsertDevice(accountId, new CachedDevice(Guid.NewGuid(), locationId, "location-1", "device-2", "Garage"));

        Assert.NotNull(cache.FindDevice(accountId, "device-1"));
        Assert.NotNull(cache.FindDevice(accountId, "device-2"));
        Assert.Single(cache.Get(accountId)!.Locations);
    }

    [Fact]
    public async Task ConcurrentReplaceAndRead_NeverSeesPartialSnapshot()
    {
        var cache = new ReferenceDataCache();
        var accountId = Guid.NewGuid();
        cache.Replace(accountId, Snapshot(Guid.NewGuid(), "device-1"));

        Task writer = Task.Run(() =>
        {
            for (int i = 0; i < 2000; i++)
            {
                cache.Replace(accountId, Snapshot(Guid.NewGuid(), "device-" + (i % 5)));
            }
        });

        Task reader = Task.Run(() =>
        {
            for (int i = 0; i < 2000; i++)
            {
                AccountReferenceData? data = cache.Get(accountId);

                // Each snapshot has one location and one device linked to it; a torn read would break that pairing.
                Assert.NotNull(data);
                Assert.Single(data.Locations);
                Assert.Single(data.DevicesByProviderId);
                Assert.Equal(data.Locations[0].Id, data.DevicesByProviderId.Values.Single().LocationId);
            }
        });

        await Task.WhenAll(writer, reader);
    }
}
