namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Ui.Shared.Extensions;
using Xunit;

public class NavigationManagerExtensionsTests : BunitContext
{
    [Fact]
    public void SignInPathWithReturnUrl_CurrentPageWithQuery_ReturnsSignInWithEscapedReturnPath()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/cases/42?tab=x");

        Assert.Equal(
            "/signin?returnUrl=" + Uri.EscapeDataString("/cases/42?tab=x"),
            nav.SignInPathWithReturnUrl());
    }

    [Fact]
    public void SignInPathWithReturnUrl_RootPage_ReturnsSignInWithSlashReturnUrl()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/");

        Assert.Equal("/signin?returnUrl=%2F", nav.SignInPathWithReturnUrl());
    }
}
