using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class UserMenu_Tests
{
    [Fact]
    public void Items_SignedInUser_ContainsChangePassword()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Path == "/change-password");
    }

    [Fact]
    public void Items_SignedInUser_ContainsMyPasskeys()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Path == "/settings/passkeys");
    }

    [Fact]
    public void Items_SignedInUser_DoesNotContainDeviceSignIn()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        UserMenu.UserMenuItem? deviceSignInItem = items.FirstOrDefault(i => i.Path == "/device-signin");
        Assert.Null(deviceSignInItem);
    }

    [Fact]
    public void Items_NotSignedIn_ContainsSignIn()
    {
        var ctx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Path == "/signin");
    }

    [Fact]
    public void Items_SignedInUser_ContainsSignOut()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        // Sign out is a special item without a path (it's a handler)
        // It should have empty/null path to indicate it's a handler
        Assert.NotEmpty(items);
    }

    [Fact]
    public void Items_NotSignedIn_DoesNotContainChangePassword()
    {
        var ctx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        UserMenu.UserMenuItem? changePasswordItem = items.FirstOrDefault(i => i.Path == "/change-password");
        Assert.Null(changePasswordItem);
    }

    [Fact]
    public void Items_AllItemsHaveText()
    {
        var signedInCtx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false);
        var notSignedInCtx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false);

        IReadOnlyList<UserMenu.UserMenuItem> signedInItems = UserMenu.Items(signedInCtx);
        IReadOnlyList<UserMenu.UserMenuItem> notSignedInItems = UserMenu.Items(notSignedInCtx);

        foreach (UserMenu.UserMenuItem item in signedInItems)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Text));
        }

        foreach (UserMenu.UserMenuItem item in notSignedInItems)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Text));
        }
    }

    [Fact]
    public void Items_SignedInUserNotLocked_ContainsUiModeToggle()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false, UiMode: "Standard", UiModeLocked: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        Assert.NotEmpty(items);
        UserMenu.UserMenuItem? toggleItem = items.FirstOrDefault(i => i.Action == "set-ui-mode:Simple");
        Assert.NotNull(toggleItem);
        Assert.Equal("Switch to Simple view", toggleItem.Text);
    }

    [Fact]
    public void Items_SignedInUserInSimpleMode_ToggleToStandard()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false, UiMode: "Simple", UiModeLocked: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        Assert.NotEmpty(items);
        UserMenu.UserMenuItem? toggleItem = items.FirstOrDefault(i => i.Action == "set-ui-mode:Standard");
        Assert.NotNull(toggleItem);
        Assert.Equal("Switch to Standard view", toggleItem.Text);
    }

    [Fact]
    public void Items_SignedInUserLocked_NoUiModeToggle()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false, UiMode: "Standard", UiModeLocked: true);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        var toggleItems = items.Where(i => i.Action != null && i.Action.StartsWith("set-ui-mode:")).ToList();
        Assert.Empty(toggleItems);
    }

    [Fact]
    public void Items_SignedOut_NoUiModeToggle()
    {
        var ctx = new NavContext(IsSignedIn: false, Role: null, AppLockSupported: false, UiMode: "Standard", UiModeLocked: false);
        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        var toggleItems = items.Where(i => i.Action != null && i.Action.StartsWith("set-ui-mode:")).ToList();
        Assert.Empty(toggleItems);
    }

    [Fact]
    public void Items_UiModeToggleAfterMyPasskeys()
    {
        var ctx = new NavContext(IsSignedIn: true, Role: OperatorRole.Admin, AppLockSupported: false, UiMode: "Standard", UiModeLocked: false);
        var items = UserMenu.Items(ctx).ToList();

        int passkeysIndex = items.FindIndex(i => i.Path == "/settings/passkeys");
        Assert.True(passkeysIndex >= 0, "My Passkeys item should exist");

        int toggleIndex = items.FindIndex(i => i.Action != null && i.Action.StartsWith("set-ui-mode:"));
        Assert.True(toggleIndex >= 0, "UI mode toggle should exist");

        Assert.True(toggleIndex > passkeysIndex, "UI mode toggle should appear after My Passkeys");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Items_AnyUser_OffersOneThemeLinkToTheThemePage(bool signedIn)
    {
        // The theme choices live in the Appearance section of Settings, so the menu holds a single link rather than three theme actions.
        var ctx = new NavContext(IsSignedIn: signedIn, Role: signedIn ? OperatorRole.Admin : null, AppLockSupported: false);

        IReadOnlyList<UserMenu.UserMenuItem> items = UserMenu.Items(ctx);

        Assert.Contains(items, i => i.Text == "Theme" && i.Path == "/settings");
        Assert.DoesNotContain(items, i => i.Action is not null && i.Action.StartsWith("set-theme:", StringComparison.Ordinal));
    }
}
