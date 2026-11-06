using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Layout;

public class ServicesPageAccess_Tests
{
    private static NavContext Ctx(OperatorRole? role, bool local = false, RestrictedSettings open = RestrictedSettings.None)
    {
        return new(true, role, AppLockSupported: false, IsLocalTier: local, OpenToAdmin: open);
    }

    [Fact]
    public void IsAllowed_ServicesPage_SuperAdmin_Allowed()
    {
        Assert.True(PageAccessRules.IsAllowed("/settings/services", Ctx(OperatorRole.SuperAdmin)));
    }

    [Fact]
    public void IsAllowed_ServicesPage_AdminByDefault_Denied()
    {
        Assert.False(PageAccessRules.IsAllowed("/settings/services", Ctx(OperatorRole.Admin)));
    }

    [Fact]
    public void IsAllowed_ServicesPage_AdminOnceSuperAdminOpensIt_Allowed()
    {
        Assert.True(PageAccessRules.IsAllowed("/settings/services", Ctx(OperatorRole.Admin, open: RestrictedSettings.Services)));
    }

    [Fact]
    public void IsAllowed_ServicesPage_OtherAreaOpened_StaysDenied()
    {
        Assert.False(PageAccessRules.IsAllowed("/settings/services", Ctx(OperatorRole.Admin, open: RestrictedSettings.Reports)));
    }

    [Fact]
    public void NavGroups_Admin_ServicesItemUnderSystemAndNetwork_SuperAdminOnly()
    {
        NavGroup admin = NavGroups.All.Single(g => g.Key == "admin");
        NavItem? item = admin.Items.SingleOrDefault(i => i.Path == "/settings/services");

        Assert.NotNull(item);
        Assert.Equal("System & Network", item!.Section);
        Assert.False(item.IsVisible(Ctx(OperatorRole.Admin)));
        Assert.True(item.IsVisible(Ctx(OperatorRole.SuperAdmin)));
    }
}
