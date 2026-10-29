using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Layout;

public class ReportSettingsGateTests
{
    private static NavContext Operator(OperatorRole role)
    {
        return new NavContext(IsSignedIn: true, Role: role, AppLockSupported: false);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SuperAdmin")]
    [InlineData("not-a-level")]
    public void CanChange_DefaultOrUnknownLevel_OnlySuperAdminCanChange(string? level)
    {
        Assert.True(ReportSettingsGate.CanChange(level, Operator(OperatorRole.SuperAdmin)));
        Assert.False(ReportSettingsGate.CanChange(level, Operator(OperatorRole.Admin)));
    }

    [Fact]
    public void CanChange_AdminLevel_AdminCanChange()
    {
        Assert.True(ReportSettingsGate.CanChange(ProviderEditAccessLevels.Admin, Operator(OperatorRole.Admin)));
    }

    [Fact]
    public void CanChange_AdminLevel_ReviewCannotChange()
    {
        Assert.False(ReportSettingsGate.CanChange(ProviderEditAccessLevels.Admin, Operator(OperatorRole.Review)));
    }
}
