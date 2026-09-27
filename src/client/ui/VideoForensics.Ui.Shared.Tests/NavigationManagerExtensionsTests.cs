namespace VideoForensics.Ui.Shared.Tests;

using Moq;
using Xunit;
using Microsoft.AspNetCore.Components;
using VideoForensics.Ui.Shared.Extensions;
using VideoForensics.Ui.Shared.Services;
using Microsoft.JSInterop;
using System.Reflection;

public class NavigationManagerExtensions_Tests
{
    [Fact]
    public void SignInPathWithContext_FreshLogin_ContextIsSignon()
    {
        // Arrange
        var navigationManager = CreateNavigationManagerWithUri("http://example.com/devices");
        var session = CreateMockPairedSessionState();
        const bool wasSignedIn = false;

        // Act
        var result = navigationManager.SignInPathWithContext(session, wasSignedIn);

        // Assert
        Assert.Contains("context=signin", result);
        Assert.Contains("returnUrl=", result);
        Assert.Equal("/devices", session.AuthReturnUrl);
        Assert.Equal("signin", session.AuthContext);
    }

    [Fact]
    public void SignInPathWithContext_ReAuth_ContextIsChallenge()
    {
        // Arrange
        var navigationManager = CreateNavigationManagerWithUri("http://example.com/cases");
        var session = CreateMockPairedSessionState();
        const bool wasSignedIn = true;

        // Act
        var result = navigationManager.SignInPathWithContext(session, wasSignedIn);

        // Assert
        Assert.Contains("context=challenge", result);
        Assert.Contains("returnUrl=", result);
        Assert.Equal("/cases", session.AuthReturnUrl);
        Assert.Equal("challenge", session.AuthContext);
    }

    [Fact]
    public void SignInPathWithContext_ReturnUrlIsEscaped()
    {
        // Arrange
        var navigationManager = CreateNavigationManagerWithUri("http://example.com/path?query=value");
        var session = CreateMockPairedSessionState();

        // Act
        var result = navigationManager.SignInPathWithContext(session);

        // Assert
        var escaped = Uri.EscapeDataString("/path?query=value");
        Assert.Contains(escaped, result);
    }

    [Fact]
    public void SignInPathWithContext_DefaultWasSignedInIsFalse()
    {
        // Arrange
        var navigationManager = CreateNavigationManagerWithUri("http://example.com/");
        var session = CreateMockPairedSessionState();

        // Act
        var result = navigationManager.SignInPathWithContext(session);

        // Assert
        Assert.Contains("context=signin", result);
    }

    private static NavigationManager CreateNavigationManagerWithUri(string uri)
    {
        // Create a partially mocked NavigationManager that works with non-virtual members
        var mock = new Mock<NavigationManager>();

        // Use reflection to set internal state that makes ToBaseRelativePath work predictably
        // Since we can't mock it, we'll use a simple approach: create an adapter
        return new TestNavigationManagerAdapter(uri);
    }

    private static PairedSessionState CreateMockPairedSessionState()
    {
        var jsMock = new Mock<IJSRuntime>();
        return new PairedSessionState(jsMock.Object);
    }

    /// <summary>
    /// Simple adapter that wraps a URI and implements ToBaseRelativePath correctly
    /// for testing purposes. This avoids the mocking limitations of NavigationManager.
    /// </summary>
    private class TestNavigationManagerAdapter : NavigationManager
    {
        private readonly string _uri;

        public TestNavigationManagerAdapter(string uri)
        {
            _uri = uri;
        }

        public override string Uri
        {
            get { return _uri; }
        }

        public override string ToBaseRelativePath(string uri)
        {
            // Correctly implement the path extraction logic
            if (string.IsNullOrEmpty(uri))
                return string.Empty;

            var uriObj = new Uri(uri, UriKind.RelativeOrAbsolute);
            if (!uriObj.IsAbsoluteUri)
                return uri;

            var path = uriObj.PathAndQuery;
            if (path.StartsWith("/"))
                path = path.Substring(1);

            return path;
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            // No-op for testing
        }
    }
}
