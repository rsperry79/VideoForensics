using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Layout;

public class ProviderAccountEditGateTests
{
    private static NavContext Operator(OperatorRole role)
    {
        return new NavContext(IsSignedIn: true, Role: role, AppLockSupported: false);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Admin")]
    [InlineData("not-a-level")]
    public void CanEdit_AdminOrUnknownLevel_AdminCanEdit(string? level)
    {
        Assert.True(ProviderAccountEditGate.CanEdit(level, Operator(OperatorRole.Admin)));
    }

    [Fact]
    public void CanEdit_SuperAdminLevel_AdminCannotEdit()
    {
        Assert.False(ProviderAccountEditGate.CanEdit(ProviderEditAccessLevels.SuperAdmin, Operator(OperatorRole.Admin)));
    }

    [Fact]
    public void CanEdit_SuperAdminLevel_SuperAdminCanEdit()
    {
        Assert.True(ProviderAccountEditGate.CanEdit(ProviderEditAccessLevels.SuperAdmin, Operator(OperatorRole.SuperAdmin)));
    }

    [Fact]
    public void CanEdit_AdminLevel_ReadOnlyCannotEdit()
    {
        Assert.False(ProviderAccountEditGate.CanEdit(ProviderEditAccessLevels.Admin, Operator(OperatorRole.ReadOnly)));
    }

    [Fact]
    public void CanEdit_NotSignedIn_CannotEdit()
    {
        var anonymous = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);

        Assert.False(ProviderAccountEditGate.CanEdit(ProviderEditAccessLevels.Admin, anonymous));
    }
}
