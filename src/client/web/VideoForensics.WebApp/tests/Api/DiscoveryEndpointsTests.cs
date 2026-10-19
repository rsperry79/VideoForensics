namespace VideoForensics.WebApp.Tests.Api;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.WebApp.Api;
using Xunit;

/// <summary>Discovery reads are served from stored rows; they never call the provider.</summary>
public class DiscoveryEndpointsTests
{
    [Fact]
    public async Task GetLocations_ReadsStoredRows_NeverCallsProviderDiscovery()
    {
        Mock<IStoredDeviceReader> reader = new();
        Mock<IDeviceDiscoveryService> provider = new();
        _ = reader.Setup(r => r.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Location("prov-home", "Home")]);

        IResult result = await DiscoveryEndpoints.GetLocations(reader.Object, CancellationToken.None);

        _ = Assert.IsType<Ok<IEnumerable<LocationDto>>>(result);
        reader.Verify(r => r.GetLocationsAsync(It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(p => p.GetLocationsAsync(It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(p => p.GetLocationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
