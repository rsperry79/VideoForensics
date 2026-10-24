using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.DeviceSettings;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.DeviceSettings;

public class DeviceConfigFilter_Tests
{
    private static Device Dev(string name, string? provider)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            ProviderDeviceId = name,
            Name = name,
            Type = "doorbot",
            ProviderName = provider,
        };
    }

    [Fact]
    public void DeviceConfigFilter_ProviderOptions_ListsDistinctSortedProviderNames()
    {
        Device[] devices = new[] { Dev("a", "Wyze"), Dev("b", "Ring"), Dev("c", "Ring"), Dev("d", null) };

        IReadOnlyList<string> options = DeviceConfigFilter.ProviderOptions(devices);

        Assert.Equal(new[] { "Ring", "Wyze" }, options);
    }

    [Fact]
    public void DeviceConfigFilter_Filter_NullProvider_ReturnsAllDevices()
    {
        Device[] devices = new[] { Dev("a", "Wyze"), Dev("b", null) };

        Assert.Equal(2, DeviceConfigFilter.Filter(devices, null).Count());
    }

    [Fact]
    public void DeviceConfigFilter_Filter_NamedProvider_ReturnsOnlyThatProvidersDevices()
    {
        Device[] devices = new[] { Dev("a", "Wyze"), Dev("b", "Ring"), Dev("c", null) };

        IReadOnlyList<Device> result = DeviceConfigFilter.Filter(devices, "Ring");

        Assert.Equal(new[] { "b" }, result.Select(d => d.Name));
    }
}
