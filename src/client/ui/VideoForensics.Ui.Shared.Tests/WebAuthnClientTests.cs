namespace VideoForensics.Ui.Shared.Tests;

using System.Net;

using Microsoft.JSInterop;

using Moq;

using VideoForensics.Ui.Shared.Services;

using Xunit;

/// <summary>
/// <see cref="WebAuthnClient"/> must send its calls through the client
/// <see cref="ISelfApiHttpClientFactory.CreateClient(string?)"/> returns, not a raw
/// <c>HttpClient</c> it builds itself - otherwise, in the WebApp, a pre-auth call like
/// <see cref="WebAuthnClient.SignInWithPasswordAsync"/> can't carry the pre-auth network-tier header
/// <c>OperatorAuthEndpoints.LoginPasswordAsync</c>'s primary-SuperAdmin Local-only check needs to
/// recover the calling circuit's real tier.
/// </summary>
public class WebAuthnClientTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? RespondWith { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(RespondWith?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FakeSelfApiHttpClientFactory : ISelfApiHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeSelfApiHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public string? LastRequestedBearerToken { get; private set; }
        public bool CreateClientNoArgCalled { get; private set; }

        public HttpClient CreateClient()
        {
            CreateClientNoArgCalled = true;
            return new HttpClient(_handler) { BaseAddress = new Uri("http://videoforensics.example.com/") };
        }

        public HttpClient CreateClient(string? bearerToken)
        {
            LastRequestedBearerToken = bearerToken;
            var client = new HttpClient(_handler) { BaseAddress = new Uri("http://videoforensics.example.com/") };
            if (bearerToken is not null)
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            return client;
        }
    }

    [Fact]
    public async Task SignInWithPasswordAsync_PreAuthCall_SendsRequestThroughFactorysClientWithNoBearerToken()
    {
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    sessionToken = "issued-token",
                    operatorId = Guid.NewGuid(),
                    role = "SuperAdmin",
                    mustChangePassword = false,
                    hasPasskey = false
                })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        (bool success, string? errorMessage, string? sessionToken, _, _, _, _, _, _) = await client.SignInWithPasswordAsync("alice", "password123");

        Assert.True(success, errorMessage);
        Assert.Equal("issued-token", sessionToken);
        Assert.Single(handler.Requests);
        Assert.EndsWith("api/v1/auth/login/password", handler.Requests[0].RequestUri!.AbsolutePath);
        // Pre-auth: no session exists yet, so the factory must be asked with a null bearer token
        // (rather than WebAuthnClient inventing its own HttpClient with no factory involvement at all).
        Assert.Null(factory.LastRequestedBearerToken);
        Assert.False(factory.CreateClientNoArgCalled);
    }

    [Fact]
    public async Task SignInWithPasswordAsync_MissingField_ThrowsNamingFieldWithoutToken()
    {
        const string token = "secret-token-value-123";
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    sessionToken = token,
                    operatorId = Guid.NewGuid(),
                    role = "SuperAdmin",
                    mustChangePassword = false
                })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SignInWithPasswordAsync("alice", "password123"));

        Assert.Contains("hasPasskey", ex.Message);
        Assert.DoesNotContain(token, ex.Message);
    }

    [Fact]
    public async Task SignInWithPasswordAsync_RequiresTwoFactor_ReturnsTwoFactorResult_WithoutToken()
    {
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    requiresTwoFactor = true,
                    twoFactorCorrelationId = "corr-123"
                })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        var result = await client.SignInWithPasswordAsync("alice", "password123");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.RequiresTwoFactor);
        Assert.Equal("corr-123", result.TwoFactorCorrelationId);
        Assert.Null(result.SessionToken);
    }

    [Fact]
    public async Task CreatePasskeyLoginAsync_Success_ReturnsSessionTuple()
    {
        var operatorId = Guid.NewGuid();
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    sessionToken = "passkey-setup-token",
                    operatorId,
                    role = "ReadOnly",
                    mustChangePassword = true,
                    hasPasskey = false,
                    requiresPasskeyRegistration = true
                })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        var result = await client.CreatePasskeyLoginAsync("alice", "password123");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("passkey-setup-token", result.SessionToken);
        Assert.Equal(operatorId, result.OperatorId);
        Assert.Equal("ReadOnly", result.Role);
        Assert.True(result.MustChangePassword);
        Assert.False(result.HasPasskey);
        Assert.Single(handler.Requests);
        Assert.EndsWith("api/v1/auth/login/password/create-passkey", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Null(factory.LastRequestedBearerToken);
    }

    [Fact]
    public async Task CreatePasskeyLoginAsync_MissingField_ThrowsNamingField()
    {
        const string token = "secret-token-value-456";
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    sessionToken = token,
                    operatorId = Guid.NewGuid(),
                    role = "ReadOnly",
                    mustChangePassword = false
                })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreatePasskeyLoginAsync("alice", "password123"));

        Assert.Contains("hasPasskey", ex.Message);
        Assert.DoesNotContain(token, ex.Message);
    }

    [Fact]
    public async Task CompleteTwoFactorWithPasskeyAsync_OptionsRequestFails_ReturnsErrorWithoutRunningPasskeyPrompt()
    {
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { error = "Invalid username or password" })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        var result = await client.CompleteTwoFactorWithPasskeyAsync("alice", "corr-123");

        Assert.False(result.Success);
        Assert.Equal("Invalid username or password", result.ErrorMessage);
        Assert.Single(handler.Requests);
        Assert.EndsWith("api/v1/auth/webauthn/username-assertion-options", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    /// <summary>JS runtime stub: returns a fixed assertion JSON for the WebAuthn ceremony call, nothing else is expected.</summary>
    private sealed class FakeAuthenticateJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult((TValue)(object)"{\"id\":\"AQID\"}");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult((TValue)(object)"{\"id\":\"AQID\"}");
    }

    private static RecordingHandler TwoFactorCompleteHandler(object body) => new()
    {
        RespondWith = request => request.RequestUri!.AbsolutePath.EndsWith("username-assertion-options", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { nonce = "nonce-1", options = new { challenge = "AAAA" } })
            }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(body) }
    };

    [Fact]
    public async Task CompleteTwoFactorWithPasskeyAsync_Success_ReadsMustChangePasswordFromResponse()
    {
        var operatorId = Guid.NewGuid();
        var handler = TwoFactorCompleteHandler(new
        {
            sessionToken = "two-factor-session-token",
            operatorId,
            role = "SuperAdmin",
            mustChangePassword = false,
            hasPasskey = true
        });
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new FakeAuthenticateJsRuntime(), factory);

        var result = await client.CompleteTwoFactorWithPasskeyAsync("alice", "corr-123");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("two-factor-session-token", result.SessionToken);
        Assert.Equal(operatorId, result.OperatorId);
        Assert.False(result.MustChangePassword);
        Assert.True(result.HasPasskey);
        Assert.EndsWith("api/v1/auth/webauthn/username-assertion-complete", handler.Requests[1].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CompleteTwoFactorWithPasskeyAsync_MissingField_ThrowsNamingField()
    {
        const string token = "secret-token-value-789";
        var handler = TwoFactorCompleteHandler(new
        {
            sessionToken = token,
            operatorId = Guid.NewGuid(),
            role = "SuperAdmin",
            hasPasskey = true
        });
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new FakeAuthenticateJsRuntime(), factory);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CompleteTwoFactorWithPasskeyAsync("alice", "corr-123"));

        Assert.Contains("mustChangePassword", ex.Message);
        Assert.DoesNotContain(token, ex.Message);
    }

    [Fact]
    public async Task ChangePasswordAsync_UsesFactoryWithTheExplicitlyPassedBearerToken()
    {
        // ChangePasswordAsync needs no WebAuthn/JS-interop ceremony, so it's the simplest explicit-
        // bearer-token method to prove this against: WebAuthnClient has no PairedSessionState of its
        // own, so it must forward the token IT was given, not something the factory derives itself.
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { sessionToken = "new-session-token" })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        (bool success, string? errorMessage, string? newSessionToken) = await client.ChangePasswordAsync("current-session-token", "old-pw", "new-pw");

        Assert.True(success, errorMessage);
        Assert.Equal("new-session-token", newSessionToken);
        Assert.Single(handler.Requests);
        Assert.EndsWith("api/v1/auth/change-password", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("current-session-token", factory.LastRequestedBearerToken);
    }

    [Fact]
    public async Task GetSetupStatusAsync_SuccessfulResponse_ParsesJsonCorrectly()
    {
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { isEmpty = true, isLocal = false })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        (bool isEmpty, bool isLocal) = await client.GetSetupStatusAsync();

        Assert.True(isEmpty);
        Assert.False(isLocal);
        Assert.Single(handler.Requests);
        Assert.EndsWith("api/v1/setup/status", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Null(factory.LastRequestedBearerToken);
    }

    [Fact]
    public async Task GetSetupStatusAsync_FailedResponse_FailsSafeToNotEmptyNotLocal()
    {
        var handler = new RecordingHandler
        {
            RespondWith = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        (bool isEmpty, bool isLocal) = await client.GetSetupStatusAsync();

        Assert.False(isEmpty);
        Assert.False(isLocal);
    }
}
