using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.WebApp.Services.CircuitGuards;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class SettingsAccessPolicy_Tests
    {
        [Theory]
        [InlineData(RestrictedSettings.Reports, "Permissions.ReportSettings")]
        [InlineData(RestrictedSettings.Email, "Permissions.EmailSettings")]
        [InlineData(RestrictedSettings.Notifications, "Permissions.NotificationSettings")]
        [InlineData(RestrictedSettings.Llm, "Permissions.LlmSettings")]
        public void SettingKeyFor_Area_KeepsTheStoredKeyName(RestrictedSettings area, string expected)
        {
            Assert.Equal(expected, SettingsAccessPolicy.SettingKeyFor(area));
        }

        [Theory]
        [InlineData(null, AccessPolicy.SuperAdmin)]
        [InlineData("", AccessPolicy.SuperAdmin)]
        [InlineData("garbage", AccessPolicy.SuperAdmin)]
        [InlineData("SuperAdmin", AccessPolicy.SuperAdmin)]
        [InlineData("Admin", AccessPolicy.Admin)]
        public async Task GetRequiredPolicyAsync_StoredValue_MapsWithSuperAdminDefault(string? stored, AccessPolicy expected)
        {
            foreach (RestrictedSettings area in new[] { RestrictedSettings.Reports, RestrictedSettings.Email, RestrictedSettings.Notifications, RestrictedSettings.Llm })
            {
                var settings = new Mock<IAppSettingRepository>();
                _ = settings.Setup(s => s.GetAsync(SettingsAccessPolicy.SettingKeyFor(area), It.IsAny<CancellationToken>())).ReturnsAsync(stored);

                AccessPolicy result = await new SettingsAccessPolicy(settings.Object).GetRequiredPolicyAsync(area, CancellationToken.None);

                Assert.Equal(expected, result);
            }
        }

        [Fact]
        public async Task GetRequiredPolicyAsync_OnlyTheOpenedAreaFollowsTheSetting()
        {
            var settings = new Mock<IAppSettingRepository>();
            _ = settings.Setup(s => s.GetAsync(SettingsAccessPolicy.SettingKeyFor(RestrictedSettings.Email), It.IsAny<CancellationToken>())).ReturnsAsync("Admin");
            var policy = new SettingsAccessPolicy(settings.Object);

            Assert.Equal(AccessPolicy.Admin, await policy.GetRequiredPolicyAsync(RestrictedSettings.Email, CancellationToken.None));
            Assert.Equal(AccessPolicy.SuperAdmin, await policy.GetRequiredPolicyAsync(RestrictedSettings.Reports, CancellationToken.None));
        }

        [Fact]
        public async Task GuardedConfigToolsOrchestrator_ReportFormat_AdminWhenSettingAllowsAdmin_CallsInner()
        {
            var inner = new Mock<VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator>();
            _ = inner.Setup(c => c.SetReportFormatAsync(It.IsAny<IForensicsConfiguration>(), "csv", It.IsAny<CancellationToken>())).ReturnsAsync((true, "ok"));
            var gate = new Mock<ICircuitOperatorGate>();
            _ = gate.Setup(g => g.Allows(AccessPolicy.Admin)).Returns(true);
            var policy = new Mock<ISettingsAccessPolicy>();
            _ = policy.Setup(p => p.GetRequiredPolicyAsync(RestrictedSettings.Reports, It.IsAny<CancellationToken>())).ReturnsAsync(AccessPolicy.Admin);
            var guarded = new GuardedConfigToolsOrchestrator(inner.Object, gate.Object, null, policy.Object);

            (bool success, _) = await guarded.SetReportFormatAsync(new Mock<IForensicsConfiguration>().Object, "csv");

            Assert.True(success);
        }

        [Fact]
        public async Task GuardedConfigToolsOrchestrator_ReportToggle_AdminWhenSettingIsSuperAdmin_ReturnsFailureAndDoesNotChange()
        {
            var inner = new Mock<VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator>();
            var gate = new Mock<ICircuitOperatorGate>();
            _ = gate.Setup(g => g.Allows(AccessPolicy.Admin)).Returns(true);
            _ = gate.Setup(g => g.Allows(AccessPolicy.SuperAdmin)).Returns(false);
            var policy = new Mock<ISettingsAccessPolicy>();
            _ = policy.Setup(p => p.GetRequiredPolicyAsync(RestrictedSettings.Reports, It.IsAny<CancellationToken>())).ReturnsAsync(AccessPolicy.SuperAdmin);
            var guarded = new GuardedConfigToolsOrchestrator(inner.Object, gate.Object, null, policy.Object);

            (bool success, _) = await guarded.SetReportEnabledAsync(new Mock<IForensicsConfiguration>().Object, "ForensicAnalysis", true);

            Assert.False(success);
            inner.Verify(c => c.SetReportEnabledAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
