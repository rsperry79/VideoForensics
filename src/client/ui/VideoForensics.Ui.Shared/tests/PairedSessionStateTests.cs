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

    [Fact]
    public async Task PairedSessionState_RefreshPasskeySetupOnlyAsync_ServerReportsSessionEnded_ClearsSetupFlag()
    {
        // Arrange - loaded as setup-only (server said so), then the server ends the session after passkey registration
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"SuperAdmin\",\"MustChangePassword\":false,\"PasskeySetupOnly\":true}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        string body = "{\"passkeySetupOnly\":true}";
        var factory = new Mock<ISelfApiHttpClientFactory>();
        _ = factory.Setup(f => f.CreateClient(It.IsAny<string?>()))
            .Returns(() => new HttpClient(new StubHandler(HttpStatusCode.OK, body)) { BaseAddress = new Uri("https://localhost/") });
        var services = new ServiceCollection().AddSingleton(factory.Object).BuildServiceProvider();
        var state = new PairedSessionState(JSInterop.JSRuntime, services);
        await state.EnsureLoadedAsync();
        Assert.True(state.PasskeySetupOnly);
        body = "{\"passkeySetupOnly\":false}";

        // Act
        await state.RefreshPasskeySetupOnlyAsync();

        // Assert
        Assert.False(state.PasskeySetupOnly);
    }

    [Fact]
    public async Task PairedSessionState_RefreshPasskeySetupOnlyAsync_SessionKindCallFails_KeepsStoredFlag()
    {
        // Arrange
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"SuperAdmin\",\"MustChangePassword\":false,\"PasskeySetupOnly\":true}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (_, services) = CreateFactory(HttpStatusCode.InternalServerError, "{}");
        var state = new PairedSessionState(JSInterop.JSRuntime, services);
        await state.EnsureLoadedAsync();

        // Act
        await state.RefreshPasskeySetupOnlyAsync();

        // Assert
        Assert.True(state.PasskeySetupOnly);
        Assert.True(state.IsSignedIn);
    }

    [Fact]
    public async Task PairedSessionState_ConfirmSessionAsync_ServerReturns401_ReturnsFalse()
    {
        // Arrange
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"SuperAdmin\",\"MustChangePassword\":false}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (_, services) = CreateFactory(HttpStatusCode.Unauthorized, "{}");
        var state = new PairedSessionState(JSInterop.JSRuntime, services);
        await state.EnsureLoadedAsync();

        // Act
        bool result = await state.ConfirmSessionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task PairedSessionState_ConfirmSessionAsync_ServerReturns200_ReturnsTrue()
    {
        // Arrange
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"SuperAdmin\",\"MustChangePassword\":false}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (_, services) = CreateFactory(HttpStatusCode.OK, "{\"passkeySetupOnly\":false}");
        var state = new PairedSessionState(JSInterop.JSRuntime, services);
        await state.EnsureLoadedAsync();

        // Act
        bool result = await state.ConfirmSessionAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task PairedSessionState_ConfirmSessionAsync_NetworkFailure_ReturnsTrue()
    {
        // Arrange - the call throws; a transient failure must not sign the operator out
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"SuperAdmin\",\"MustChangePassword\":false}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var factory = new Mock<ISelfApiHttpClientFactory>();
        _ = factory.Setup(f => f.CreateClient(It.IsAny<string?>())).Throws(new HttpRequestException("network down"));
        var services = new ServiceCollection().AddSingleton(factory.Object).BuildServiceProvider();
        var state = new PairedSessionState(JSInterop.JSRuntime, services);
        await state.EnsureLoadedAsync();

        // Act
        bool result = await state.ConfirmSessionAsync();

        // Assert
        Assert.True(result);
        Assert.True(state.IsSignedIn);
    }

    [Fact]
    public async Task PairedSessionState_ServerReportsLocalTier_SetsIsLocalTierAndKnown()
    {
        // Arrange - the server answers from this request's real connection, so Local means the operator is physically local
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"SuperAdmin\",\"MustChangePassword\":false}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (_, services) = CreateFactory(HttpStatusCode.OK, "{\"passkeySetupOnly\":false,\"localTier\":true,\"passkeySession\":false}");
        var state = new PairedSessionState(JSInterop.JSRuntime, services);

        // Act
        await state.EnsureLoadedAsync();

        // Assert
        Assert.True(state.IsLocalTier);
        Assert.False(state.IsPasskeySession);
        Assert.True(state.ServerKindKnown);
    }

    [Fact]
    public async Task PairedSessionState_ServerOmitsTier_FailsClosedToNotLocal()
    {
        // Arrange - an older server that does not report the tier must not unlock Local-only pages
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"SuperAdmin\",\"MustChangePassword\":false}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (_, services) = CreateFactory(HttpStatusCode.OK, "{\"passkeySetupOnly\":false}");
        var state = new PairedSessionState(JSInterop.JSRuntime, services);

        // Act
        await state.EnsureLoadedAsync();

        // Assert
        Assert.False(state.IsLocalTier);
        Assert.True(state.ServerKindKnown);
    }

    [Fact]
    public async Task PairedSessionState_ResolvedFromDi_WithSelfApiFactory_ReceivesServerTier()
    {
        // MAUI registers PairedSessionState with AddScoped<T>() and the default self-API factory. The container must
        // pick the constructor that takes IServiceProvider, or the tier is never asked for and MAUI stays fail-closed.
        string stored = "{\"SessionToken\":\"token\",\"OperatorId\":\"" + Guid.NewGuid() + "\",\"Role\":\"SuperAdmin\",\"MustChangePassword\":false}";
        JSInterop.Setup<string?>("vfWebAuthn.loadSession").SetResult(stored);
        var (_, factoryServices) = CreateFactory(HttpStatusCode.OK, "{\"passkeySetupOnly\":false,\"localTier\":true,\"passkeySession\":false}");
        var container = new ServiceCollection();
        container.AddSingleton(JSInterop.JSRuntime);
        container.AddSingleton(factoryServices.GetRequiredService<ISelfApiHttpClientFactory>());
        container.AddScoped<PairedSessionState>();
        await using ServiceProvider provider = container.BuildServiceProvider();
        PairedSessionState state = provider.GetRequiredService<PairedSessionState>();

        // Act
        await state.EnsureLoadedAsync();

        // Assert
        Assert.True(state.IsLocalTier);
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
