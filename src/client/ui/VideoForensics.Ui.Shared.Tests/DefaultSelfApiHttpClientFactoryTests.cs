namespace VideoForensics.Ui.Shared.Tests;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Ui.Shared.Services;

using Xunit;

/// <summary>
/// <see cref="DefaultSelfApiHttpClientFactory"/> must reproduce, byte for byte, the behavior every
/// <c>Pages/Security*.razor</c> page's own private <c>CreateClient()</c> had before this abstraction
/// existed: <see cref="HttpClient.BaseAddress"/> from <see cref="NavigationManager.BaseUri"/>, and a
/// bearer <c>Authorization</c> header from <see cref="PairedSessionState.SessionToken"/> only when a
/// token is present.
/// </summary>
public class DefaultSelfApiHttpClientFactoryTests
{
    /// <summary>Minimal concrete NavigationManager for tests - the base class only exposes
    /// BaseUri/Uri via the protected Initialize method.</summary>
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string baseUri)
        {
            Initialize(baseUri, baseUri);
        }
    }

    private static PairedSessionState MakeSessionState() => new(new Mock<IJSRuntime>().Object);

    [Fact]
    public void CreateClient_SetsBaseAddressFromNavigationManager()
    {
        var navigationManager = new TestNavigationManager("https://videoforensics.example.com/");
        var factory = new DefaultSelfApiHttpClientFactory(navigationManager, MakeSessionState());

        HttpClient client = factory.CreateClient();

        Assert.Equal(new Uri("https://videoforensics.example.com/"), client.BaseAddress);
    }

    [Fact]
    public async Task CreateClient_SessionSignedIn_AttachesBearerToken()
    {
        var navigationManager = new TestNavigationManager("https://videoforensics.example.com/");
        var sessionState = MakeSessionState();
        await sessionState.SetAsync("test-session-token", Guid.NewGuid(), "SuperAdmin");
        var factory = new DefaultSelfApiHttpClientFactory(navigationManager, sessionState);

        HttpClient client = factory.CreateClient();

        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal("test-session-token", client.DefaultRequestHeaders.Authorization!.Parameter);
    }

    [Fact]
    public void CreateClient_NotSignedIn_AttachesNoAuthorizationHeader()
    {
        var navigationManager = new TestNavigationManager("https://videoforensics.example.com/");
        var factory = new DefaultSelfApiHttpClientFactory(navigationManager, MakeSessionState());

        HttpClient client = factory.CreateClient();

        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void CreateClient_CalledTwice_ReturnsDistinctClientInstancesAndDisposingOneLeavesTheOtherUsable()
    {
        var navigationManager = new TestNavigationManager("https://videoforensics.example.com/");
        var factory = new DefaultSelfApiHttpClientFactory(navigationManager, MakeSessionState());

        HttpClient first = factory.CreateClient();
        HttpClient second = factory.CreateClient();

        Assert.NotSame(first, second);

        // Disposing one fresh client must never affect a later one (each call builds its own
        // handler(s) rather than sharing state across calls).
        first.Dispose();
        Assert.Equal(new Uri("https://videoforensics.example.com/"), second.BaseAddress);
        second.DefaultRequestHeaders.Add("X-Probe", "still-usable");
        Assert.Contains("still-usable", second.DefaultRequestHeaders.GetValues("X-Probe"));
    }
}
