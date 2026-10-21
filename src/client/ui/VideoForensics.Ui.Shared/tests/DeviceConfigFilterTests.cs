namespace VideoForensics.Ui.Shared.Tests;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services;
using Xunit;

public class DeviceConfigFilter_Tests
{
    private static Device Dev(string name, string? provider) => new()
    {
        Id = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        ProviderDeviceId = name,
        Name = name,
        Type = "doorbot",
        ProviderName = provider,
    };

    [Fact]
    public void DeviceConfigFilter_ProviderOptions_ListsDistinctSortedProviderNames()
    {
        var devices = new[] { Dev("a", "Wyze"), Dev("b", "Ring"), Dev("c", "Ring"), Dev("d", null) };

        IReadOnlyList<string> options = DeviceConfigFilter.ProviderOptions(devices);

        Assert.Equal(new[] { "Ring", "Wyze" }, options);
    }

    [Fact]
    public void DeviceConfigFilter_Filter_NullProvider_ReturnsAllDevices()
    {
        var devices = new[] { Dev("a", "Wyze"), Dev("b", null) };

        Assert.Equal(2, DeviceConfigFilter.Filter(devices, null).Count());
    }

    [Fact]
    public void DeviceConfigFilter_Filter_NamedProvider_ReturnsOnlyThatProvidersDevices()
    {
        var devices = new[] { Dev("a", "Wyze"), Dev("b", "Ring"), Dev("c", null) };

        IReadOnlyList<Device> result = DeviceConfigFilter.Filter(devices, "Ring");

        Assert.Equal(new[] { "b" }, result.Select(d => d.Name));
    }
}
