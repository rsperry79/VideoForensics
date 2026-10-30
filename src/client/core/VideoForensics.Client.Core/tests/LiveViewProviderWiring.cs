using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Core.Tests
{
    /// <summary>
    /// Wires a mocked scope provider the way production resolves live view: the device's location belongs to a Ring
    /// account, and <see cref="ILiveViewCapableProvider"/> is resolved as a collection.
    /// </summary>
    internal static class LiveViewProviderWiring
    {
        public static void Register(Mock<IServiceProvider> sp, Mock<ILiveViewCapableProvider> ringProvider)
        {
            _ = ringProvider.SetupGet(p => p.ProviderName).Returns("Ring");

            var locations = new Mock<ILocationRepository>();
            _ = locations
                .Setup(r => r.ListProviderAccountsForLocationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new ProviderAccount { ProviderName = "Ring" }]);

            _ = sp.Setup(s => s.GetService(typeof(ILocationRepository))).Returns(locations.Object);
            _ = sp.Setup(s => s.GetService(typeof(IEnumerable<ILiveViewCapableProvider>))).Returns(new[] { ringProvider.Object });
        }
    }
}
