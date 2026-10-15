using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// <see cref="WebAppSelfApiHttpClientFactory"/> is the WebApp's <see cref="ISelfApiHttpClientFactory"/>
    /// registration for <c>Pages/Security*.razor</c>: it must produce a client wired exactly like
    /// <see cref="SelfHttpServiceExtensions.AddSelfHttpService{TService}"/>'s existing chain - the
    /// <c>X-VF-Session-Tier</c> header (round-tripping through <see cref="ISessionTierHeaderProtector"/>
    /// with the session's real tier and operator) attached before the bearer token, and the
    /// bearer token itself - so those pages get the same SuperAdminLocal-safe behavior
    /// RemoteSecurityEventsService/RemoteAdminOperatorService already get.
    /// </summary>
    public class WebAppSelfApiHttpClientFactoryTests
    {
        private sealed class TestNavigationManager : NavigationManager
        {
            public TestNavigationManager(string baseUri)
            {
                Initialize(baseUri, baseUri);
            }
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }
        }

        private static (IServiceProvider provider, RecordingHandler recorder) BuildProvider(
            string sessionToken,
            Guid operatorId,
            NetworkTier tier,
            string protectedHeaderValue)
        {
            var services = new ServiceCollection();

            var sessionState = new PairedSessionState(new Mock<IJSRuntime>().Object);
            sessionState.SetAsync(sessionToken, operatorId, OperatorRole.SuperAdmin.ToString()).GetAwaiter().GetResult();
            services.AddScoped(_ => sessionState);

            services.AddScoped<NavigationManager>(_ => new TestNavigationManager("https://videoforensics.example.com/"));

            var networkContext = new SessionNetworkContext();
            networkContext.SetTier(tier);
            services.AddScoped(_ => networkContext);

            var principal = new SessionPrincipal(operatorId, null, CredentialKind.Password, OperatorRole.SuperAdmin, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            var tokenService = new Mock<ISessionTokenService>();
            tokenService.Setup(t => t.Validate(sessionToken)).Returns(principal);
            services.AddSingleton(tokenService.Object);

            var protector = new Mock<ISessionTierHeaderProtector>();
            protector.Setup(p => p.Protect(tier, operatorId)).Returns(protectedHeaderValue);
            services.AddSingleton(protector.Object);

            var recorder = new RecordingHandler();
            services.AddScoped(sp => new WebAppSelfApiHttpClientFactory(sp, recorder));

            return (services.BuildServiceProvider(), recorder);
        }

        [Fact]
        public async Task CreateClient_SendingRequest_AttachesProtectedSessionTierHeaderAndBearerToken()
        {
            var operatorId = Guid.NewGuid();
            (IServiceProvider provider, RecordingHandler recorder) = BuildProvider(
                sessionToken: "test-session-token",
                operatorId: operatorId,
                tier: NetworkTier.Internet,
                protectedHeaderValue: "protected-tier-value");

            using IServiceScope scope = provider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<WebAppSelfApiHttpClientFactory>();

            using HttpClient client = factory.CreateClient();
            _ = await client.GetAsync("/api/v1/lockout-policy");

            Assert.NotNull(recorder.LastRequest);
            Assert.True(recorder.LastRequest!.Headers.TryGetValues(SessionTierHeaderNames.HeaderName, out IEnumerable<string>? values));
            Assert.Equal("protected-tier-value", values!.Single());
            Assert.NotNull(recorder.LastRequest.Headers.Authorization);
            Assert.Equal("Bearer", recorder.LastRequest.Headers.Authorization!.Scheme);
            Assert.Equal("test-session-token", recorder.LastRequest.Headers.Authorization!.Parameter);
        }

        [Fact]
        public void CreateClient_SetsBaseAddressFromNavigationManager()
        {
            (IServiceProvider provider, _) = BuildProvider(
                sessionToken: "test-session-token",
                operatorId: Guid.NewGuid(),
                tier: NetworkTier.Local,
                protectedHeaderValue: "protected-tier-value");

            using IServiceScope scope = provider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<WebAppSelfApiHttpClientFactory>();

            using HttpClient client = factory.CreateClient();

            Assert.Equal(new Uri("https://videoforensics.example.com/"), client.BaseAddress);
        }

        [Fact]
        public void CreateClient_ImplementsISelfApiHttpClientFactory()
        {
            (IServiceProvider provider, _) = BuildProvider(
                sessionToken: "test-session-token",
                operatorId: Guid.NewGuid(),
                tier: NetworkTier.Local,
                protectedHeaderValue: "protected-tier-value");

            using IServiceScope scope = provider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<WebAppSelfApiHttpClientFactory>();

            Assert.IsAssignableFrom<ISelfApiHttpClientFactory>(factory);
        }
    }
}
