using Moq;

using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class ProviderAccountEditPolicy_Tests
    {
        [Fact]
        public async Task ProviderAccountEditPolicy_NoStoredSetting_DefaultsToAdmin()
        {
            var settings = new Mock<IAppSettingRepository>();
            _ = settings.Setup(s => s.GetAsync(ProviderAccountEditPolicy.SettingKey, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
            var policy = new ProviderAccountEditPolicy(settings.Object);

            AccessPolicy required = await policy.GetRequiredPolicyAsync(CancellationToken.None);

            Assert.Equal(AccessPolicy.Admin, required);
        }

        [Fact]
        public async Task ProviderAccountEditPolicy_StoredSuperAdmin_RequiresSuperAdmin()
        {
            var settings = new Mock<IAppSettingRepository>();
            _ = settings.Setup(s => s.GetAsync(ProviderAccountEditPolicy.SettingKey, It.IsAny<CancellationToken>())).ReturnsAsync("SuperAdmin");
            var policy = new ProviderAccountEditPolicy(settings.Object);

            AccessPolicy required = await policy.GetRequiredPolicyAsync(CancellationToken.None);

            Assert.Equal(AccessPolicy.SuperAdmin, required);
        }

        [Fact]
        public async Task ProviderAccountEditPolicy_UnrecognisedStoredValue_FallsBackToAdmin()
        {
            // A corrupted or hand-edited value must not lock out admins, nor open the setting to anyone below Admin.
            var settings = new Mock<IAppSettingRepository>();
            _ = settings.Setup(s => s.GetAsync(ProviderAccountEditPolicy.SettingKey, It.IsAny<CancellationToken>())).ReturnsAsync("Everyone");
            var policy = new ProviderAccountEditPolicy(settings.Object);

            AccessPolicy required = await policy.GetRequiredPolicyAsync(CancellationToken.None);

            Assert.Equal(AccessPolicy.Admin, required);
        }

        [Theory]
        [InlineData(OperatorRole.Admin, AccessPolicy.Admin, true)]
        [InlineData(OperatorRole.ReadOnly, AccessPolicy.Admin, false)]
        [InlineData(OperatorRole.Admin, AccessPolicy.SuperAdmin, false)]
        [InlineData(OperatorRole.SuperAdmin, AccessPolicy.SuperAdmin, true)]
        public void ProviderAccountEditRules_IsAllowed_ComparesRoleToRequiredPolicy(OperatorRole role, AccessPolicy required, bool expected)
        {
            Assert.Equal(expected, ProviderAccountEditRules.IsAllowed(required, role));
        }
    }
}
