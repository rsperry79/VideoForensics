namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.Ui.Shared.Layout.Mobile;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// Covers the session-expiry redirect in the desktop (MainLayout) and mobile (MobileLayout) layouts:
/// when PairedSessionState raises AuthenticationExpired, the layout must send the user to
/// /signin carrying a returnUrl back to the page they were on.
/// </summary>
public class SessionExpiryRedirectTests : UserMenuButtonTestBase
{
    [Fact]
    public async Task MobileLayout_SessionExpires_NavigatesToSignInWithReturnUrl()
    {
        // Arrange
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/cases/42?tab=x");
        var session = Services.GetRequiredService<PairedSessionState>();
        var component = Render<MobileLayout>();

        // Act
        await component.InvokeAsync(() => session.NotifyAuthenticationExpiredAsync());

        // Assert
        AssertRedirectedToSignInReturning(nav, "/cases/42?tab=x");
    }

    [Fact]
    public async Task MainLayout_SessionExpires_NavigatesToSignInWithReturnUrl()
    {
        // Arrange
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/cases/42?tab=x");
        var session = Services.GetRequiredService<PairedSessionState>();
        var component = Render<MainLayout>();

        // Act
        await component.InvokeAsync(() => session.NotifyAuthenticationExpiredAsync());

        // Assert
        AssertRedirectedToSignInReturning(nav, "/cases/42?tab=x");
    }

    private static void AssertRedirectedToSignInReturning(NavigationManager nav, string expectedReturnUrl)
    {
        Assert.Equal("/signin", new Uri(nav.Uri).AbsolutePath);

        const string marker = "returnUrl=";
        int start = nav.Uri.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected a returnUrl query parameter in '{nav.Uri}'.");

        string encoded = nav.Uri[(start + marker.Length)..];
        int amp = encoded.IndexOf('&');
        if (amp >= 0)
        {
            encoded = encoded[..amp];
        }

        Assert.Equal(expectedReturnUrl, Uri.UnescapeDataString(encoded));
    }
}
