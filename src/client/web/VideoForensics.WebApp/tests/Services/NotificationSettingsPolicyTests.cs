using Moq;

using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class NotificationSettingsPolicy_Tests
    {
        [Theory]
        [InlineData(null, AccessPolicy.SuperAdmin)]
        [InlineData("", AccessPolicy.SuperAdmin)]
        [InlineData("garbage", AccessPolicy.SuperAdmin)]
        [InlineData("SuperAdmin", AccessPolicy.SuperAdmin)]
        [InlineData("Admin", AccessPolicy.Admin)]
        public async Task GetRequiredPolicyAsync_StoredValue_MapsWithSuperAdminDefault(string? stored, AccessPolicy expected)
        {
            var settings = new Mock<IAppSettingRepository>();
            _ = settings.Setup(s => s.GetAsync(NotificationSettingsPolicy.SettingKey, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

            AccessPolicy result = await new NotificationSettingsPolicy(settings.Object).GetRequiredPolicyAsync(CancellationToken.None);

            Assert.Equal(expected, result);
        }
    }
}
