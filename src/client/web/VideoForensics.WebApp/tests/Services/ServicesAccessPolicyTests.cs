using Moq;

using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class ServicesAccessPolicy_Tests
    {
        [Fact]
        public void SettingKeyFor_Services_UsesServicesKey()
        {
            Assert.Equal("Permissions.ServicesSettings", SettingsAccessPolicy.SettingKeyFor(RestrictedSettings.Services));
        }

        [Theory]
        [InlineData(null, AccessPolicy.SuperAdmin)]
        [InlineData("garbage", AccessPolicy.SuperAdmin)]
        [InlineData("Admin", AccessPolicy.Admin)]
        public async Task GetRequiredPolicyAsync_Services_DefaultsToSuperAdmin(string? stored, AccessPolicy expected)
        {
            var settings = new Mock<IAppSettingRepository>();
            _ = settings.Setup(s => s.GetAsync("Permissions.ServicesSettings", It.IsAny<CancellationToken>())).ReturnsAsync(stored);

            AccessPolicy result = await new SettingsAccessPolicy(settings.Object).GetRequiredPolicyAsync(RestrictedSettings.Services, CancellationToken.None);

            Assert.Equal(expected, result);
        }
    }
}
