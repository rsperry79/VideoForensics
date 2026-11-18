using Bunit;
using Bunit.TestDoubles;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Components;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Components
{
    public class NetworkTierCaptureTests : BunitContext
    {
        [Fact]
        public void Render_InteractiveBranchWithPersistedTier_PersistsTierAgain()
        {
            // Arrange - interactive circuit: no HttpContext, tier recovered from persisted state
            _ = Services.AddSingleton(new Mock<INetworkTierResolver>().Object);
            _ = Services.AddScoped<SessionNetworkContext>();
            BunitPersistentComponentState persistent = this.AddBunitPersistentComponentState();
            persistent.Persist("vf-session-network-tier", NetworkTier.Local);

            // Act
            _ = Render<NetworkTierCapture>();
            persistent.TriggerOnPersisting();

            // Assert - the tier must be re-persisted so it survives a further circuit restart
            Assert.True(persistent.TryTake("vf-session-network-tier", out NetworkTier tier));
            Assert.Equal(NetworkTier.Local, tier);
        }
    }
}
