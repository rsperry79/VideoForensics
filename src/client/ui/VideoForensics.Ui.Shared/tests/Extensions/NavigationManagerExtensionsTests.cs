
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Extensions;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Extensions;

public class NavigationManagerExtensionsTests : BunitContext
{
    [Fact]
    public void SignInPathWithReturnUrl_CurrentPageWithQuery_ReturnsSignInWithEscapedReturnPath()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/cases/42?tab=x");

        Assert.Equal(
            "/signin?returnUrl=" + Uri.EscapeDataString("/cases/42?tab=x"),
            nav.SignInPathWithReturnUrl());
    }

    [Fact]
    public void SignInPathWithReturnUrl_RootPage_ReturnsSignInWithSlashReturnUrl()
    {
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/");

        Assert.Equal("/signin?returnUrl=%2F", nav.SignInPathWithReturnUrl());
    }
}
