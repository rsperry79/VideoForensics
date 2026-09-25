namespace VideoForensics.Ui.Shared.Tests;

using System.Net;
using System.Net.Http.Json;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

using Xunit;

/// <summary>
/// SecurityLockoutPolicy.razor's self-HTTP calls must go through the injected
/// <see cref="ISelfApiHttpClientFactory"/> (plan §5.10/§5.12) rather than a raw
/// <c>new HttpClient { BaseAddress = NavigationManager.BaseUri }</c> - otherwise, in the WebApp, the
/// call is an untagged self-call and the server can't tell it apart from a real loopback request,
/// which would incorrectly resolve to Local tier for a remote browser user. This proves the page asks
/// a fake factory for its client and sends its request through the handler THAT factory returned,
/// rather than building its own.
/// </summary>
public class SecurityLockoutPolicyPageTests : BunitContext
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.RequestUri!.AbsolutePath.EndsWith("/two-factor-policy/roles", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<object>())
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    maxFailedAttempts = 5,
                    lockoutDurationMinutes = 30,
                    blockedCountryCodes = (string?)null,
                    failClosedOnLookupError = false
                })
            });
        }
    }

    /// <summary>
    /// Attaches the current session's bearer token like the real factories do (see
    /// DefaultSelfApiHttpClientFactory/WebAppSelfApiHttpClientFactory) - a page that instead built
    /// its own HttpClient wouldn't send THIS bearer value, since it would come from whatever
    /// PairedSessionState the page itself resolved, not this fake's.
    /// </summary>
    private sealed class FakeSelfApiHttpClientFactory : ISelfApiHttpClientFactory
    {
        private readonly RecordingHandler _handler;
        private readonly PairedSessionState _sessionState;

        public FakeSelfApiHttpClientFactory(RecordingHandler handler, PairedSessionState sessionState)
        {
            _handler = handler;
            _sessionState = sessionState;
        }

        public HttpClient CreateClient()
        {
            var client = new HttpClient(_handler) { BaseAddress = new Uri("http://videoforensics.example.com/") };
            if (_sessionState.SessionToken is not null)
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sessionState.SessionToken);
            }

            return client;
        }
    }

    private readonly RecordingHandler _handler = new();

    public SecurityLockoutPolicyPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();
    }

    private async Task<PairedSessionState> RegisterSignedInSessionAsync()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
        Services.AddScoped(_ => session);
        Services.AddScoped<ISelfApiHttpClientFactory>(_ => new FakeSelfApiHttpClientFactory(_handler, session));
        return session;
    }

    [Fact]
    public async Task PageOpen_SignedIn_SendsRequestsThroughInjectedFactorysClient()
    {
        await RegisterSignedInSessionAsync();

        Render<SecurityLockoutPolicy>();

        // The page reloads on OnAfterRenderAsync(firstRender) after SessionState.EnsureLoadedAsync
        // (see SecurityEventsPageTests' identical note) - both OnInitializedAsync's and
        // OnAfterRenderAsync's loads go through the fake factory's client and its RecordingHandler.
        Assert.Contains(
            _handler.Requests,
            r => r.RequestUri!.AbsolutePath.EndsWith("/api/v1/lockout-policy", StringComparison.Ordinal));
        Assert.Contains(
            _handler.Requests,
            r => r.RequestUri!.AbsolutePath.EndsWith("/two-factor-policy/roles", StringComparison.Ordinal));

        // Every request must carry the bearer token the fake factory's client was set up to send -
        // proof the page used THAT client (built from PairedSessionState) rather than one of its own.
        Assert.All(_handler.Requests, r =>
        {
            Assert.NotNull(r.Headers.Authorization);
            Assert.Equal("test-token", r.Headers.Authorization!.Parameter);
        });
    }
}
