namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using System.Net;
using System.Text;
using VideoForensics.Ui.Shared.Services;
using Xunit;

/// <summary>
/// Covers the passkey-setup-only flag on PairedSessionState: set by a create-passkey sign-in, persisted
/// alongside the other session fields, and cleared with the session.
/// </summary>
public class PairedSessionStateTests : BunitContext
{
    public PairedSessionStateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task PairedSessionState_SetAsyncWithPasskeySetupOnly_PersistsFlag()
    {
        // Arrange
        var state = new PairedSessionState(JSInterop.JSRuntime);

        // Act
        await state.SetAsync("token", Guid.NewGuid(), "ReadOnly", mustChangePassword: false, passkeySetupOnly: true);

        // Assert - in memory, and in the JSON handed to vfWebAuthn.saveSession so a reload restores it
        Assert.True(state.PasskeySetupOnly);
        string saved = JSInterop.Invocations.Single(i => i.Identifier == "vfWebAuthn.saveSession").Arguments[0]!.ToString()!;

        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(saved);
        var reloaded = new PairedSessionState(JSInterop.JSRuntime);
        await reloaded.EnsureLoadedAsync();
        Assert.True(reloaded.PasskeySetupOnly);
    }

    [Fact]
    public async Task PairedSessionState_ClearAsync_ClearsPasskeySetupOnly()
    {
        // Arrange
        var state = new PairedSessionState(JSInterop.JSRuntime);
        await state.SetAsync("token", Guid.NewGuid(), "ReadOnly", passkeySetupOnly: true);

        // Act
        await state.ClearAsync();

        // Assert
        Assert.False(state.PasskeySetupOnly);
    }

    [Fact]
    public async Task PairedSessionState_LoadedLimitedSession_SetsFlagFromServer()
    {
        // Arrange - stored session has no flag (e.g. written before it existed); the server says it is limited
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"ReadOnly\",\"MustChangePassword\":false}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (factory, services) = CreateFactory(HttpStatusCode.OK, "{\"passkeySetupOnly\":true}");
        var state = new PairedSessionState(JSInterop.JSRuntime, services);

        // Act
        await state.EnsureLoadedAsync();

        // Assert
        Assert.True(state.PasskeySetupOnly);
        factory.Verify(f => f.CreateClient("token"), Times.Once);
    }

    [Fact]
    public async Task PairedSessionState_ServerSaysFull_ClearsStoredFlag()
    {
        // Arrange - stored session still carries the limited flag, but the server says the session is full
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"ReadOnly\",\"MustChangePassword\":false,\"PasskeySetupOnly\":true}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (factory, services) = CreateFactory(HttpStatusCode.OK, "{\"passkeySetupOnly\":false}");
        var state = new PairedSessionState(JSInterop.JSRuntime, services);

        // Act
        await state.EnsureLoadedAsync();

        // Assert
        Assert.False(state.PasskeySetupOnly);
        Assert.True(state.IsSignedIn);
    }

    [Fact]
    public async Task PairedSessionState_EndpointFails_KeepsStoredFlag()
    {
        // Arrange - the session-kind call fails; the stored flag is the fallback and the session must stay signed in
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"ReadOnly\",\"MustChangePassword\":false,\"PasskeySetupOnly\":true}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (factory, services) = CreateFactory(HttpStatusCode.InternalServerError, "{}");
        var state = new PairedSessionState(JSInterop.JSRuntime, services);

        // Act
        await state.EnsureLoadedAsync();

        // Assert
        Assert.True(state.PasskeySetupOnly);
        Assert.True(state.IsSignedIn);
    }

    /// <summary>Builds a factory whose clients answer every request with the given status and body.</summary>
    private static (Mock<ISelfApiHttpClientFactory> Factory, IServiceProvider Services) CreateFactory(HttpStatusCode status, string body)
    {
        var factory = new Mock<ISelfApiHttpClientFactory>();
        _ = factory.Setup(f => f.CreateClient(It.IsAny<string?>()))
            .Returns(() => new HttpClient(new StubHandler(status, body)) { BaseAddress = new Uri("https://localhost/") });
        var services = new ServiceCollection().AddSingleton(factory.Object).BuildServiceProvider();
        return (factory, services);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }
}
