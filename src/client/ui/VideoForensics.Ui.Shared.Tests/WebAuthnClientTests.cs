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
                    mustChangePassword = false
                })
            }
        };
        var factory = new FakeSelfApiHttpClientFactory(handler);
        var client = new WebAuthnClient(new Mock<IJSRuntime>().Object, factory);

        (bool success, string? errorMessage, string? sessionToken, _, _, _) = await client.SignInWithPasswordAsync("alice", "password123");

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
}
