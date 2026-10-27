using Moq;

using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.WebApp.Services;

namespace VideoForensics.Client.WebApp.Tests
{
    /// <summary>Test double for the provider-account edit setting, so harnesses don't need an app-setting store.</summary>
    internal static class ProviderAccountEditPolicyStub
    {
        /// <summary>A policy that reports Admin as the required level, which is the default when nothing is stored.</summary>
        public static IProviderAccountEditPolicy Admin()
        {
            var policy = new Mock<IProviderAccountEditPolicy>();
            _ = policy.Setup(p => p.GetRequiredPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(AccessPolicy.Admin);
            return policy.Object;
        }
    }
}
