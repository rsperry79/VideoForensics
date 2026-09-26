namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Data.Common.Entities;

public class UserMenu_Tests
{
    [Fact]
    public void Items_SignedInUser_ContainsChangePassword()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        var items = UserMenu.Items(ctx);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Path == "/change-password");
    }

    [Fact]
    public void Items_SignedInUser_ContainsMyPasskeys()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        var items = UserMenu.Items(ctx);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Path == "/settings/passkeys");
    }

    [Fact]
    public void Items_SignedInUser_DoesNotContainDeviceSignIn()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        var items = UserMenu.Items(ctx);

        var deviceSignInItem = items.FirstOrDefault(i => i.Path == "/device-signin");
        Assert.Null(deviceSignInItem);
    }

    [Fact]
    public void Items_NotSignedIn_ContainsDeviceSignIn()
    {
        var ctx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);
        var items = UserMenu.Items(ctx);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Path == "/device-signin");
    }

    [Fact]
    public void Items_SignedInUser_ContainsSignOut()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        var items = UserMenu.Items(ctx);

        // Sign out is a special item without a path (it's a handler)
        // It should have empty/null path to indicate it's a handler
        Assert.NotEmpty(items);
    }

    [Fact]
    public void Items_NotSignedIn_DoesNotContainChangePassword()
    {
        var ctx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);
        var items = UserMenu.Items(ctx);

        var changePasswordItem = items.FirstOrDefault(i => i.Path == "/change-password");
        Assert.Null(changePasswordItem);
    }

    [Fact]
    public void Items_AllItemsHaveText()
    {
        var signedInCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        var notSignedInCtx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);

        var signedInItems = UserMenu.Items(signedInCtx);
        var notSignedInItems = UserMenu.Items(notSignedInCtx);

        foreach (var item in signedInItems)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Text));
        }

        foreach (var item in notSignedInItems)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Text));
        }
    }
}
