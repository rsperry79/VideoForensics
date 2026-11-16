using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Exercises GET and POST /api/v1/notifications/preferences through an in-memory TestServer:
    /// the wire JSON shape, defaults, boundary validation and the authorization requirement.
    /// </summary>
    public class NotificationPreferencesEndpointsTests
    {
        private const string TestScheme = "NotificationPreferencesTest";
        private const string PreferencesUrl = "/api/v1/notifications/preferences";
        private static readonly Guid OperatorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
                : base(options, logger, encoder) { }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (Request.Headers.ContainsKey("X-Test-Anonymous"))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                var identity = new ClaimsIdentity(
                [
                    new Claim(VideoForensicsClaimTypes.Role, "Admin"),
                    new Claim(VideoForensicsClaimTypes.NetworkTier, "Local"),
                    new Claim(VideoForensicsClaimTypes.OperatorId, OperatorId.ToString())
                ], TestScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
            }
        }

        private sealed class InMemoryPreferenceRepository : IOperatorNotificationPreferenceRepository
        {
            private readonly ConcurrentDictionary<Guid, OperatorNotificationPreference> _rows = new();

            public Task<OperatorNotificationPreference?> GetAsync(Guid operatorId, CancellationToken ct)
            {
                return Task.FromResult(_rows.TryGetValue(operatorId, out OperatorNotificationPreference? row) ? row : null);
            }

            public Task UpsertAsync(OperatorNotificationPreference preferences, CancellationToken ct)
            {
                _rows[preferences.OperatorId] = preferences;
                return Task.CompletedTask;
            }

            public bool HasRow(Guid operatorId) => _rows.ContainsKey(operatorId);
        }

        private sealed class TestHost : IAsyncDisposable
        {
            public required WebApplication App { get; init; }
            public required HttpClient Client { get; init; }
            public required InMemoryPreferenceRepository Repository { get; init; }

            public async ValueTask DisposeAsync()
            {
                Client.Dispose();
                await App.StopAsync();
                await App.DisposeAsync();
            }
        }

        private static async Task<TestHost> StartAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();

            var repository = new InMemoryPreferenceRepository();
            _ = builder.Services.AddSingleton<IOperatorNotificationPreferenceRepository>(repository);
            _ = builder.Services.AddAuthorization();
            _ = builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestScheme, _ => { });

            WebApplication app = builder.Build();
            _ = app.UseAuthentication();
            _ = app.UseAuthorization();
            app.MapNotificationPreferencesEndpoints();
            await app.StartAsync();

            return new TestHost
            {
                App = app,
                Client = app.GetTestClient(),
                Repository = repository
            };
        }

        private static object ValidBody() => new
        {
            pushEnabled = true,
            minimumSeverity = 1,
            emailEnabled = true,
            emailEventTypes = new[] { SecurityAuditEventTypes.PairingCompleted, SecurityAuditEventTypes.AuthSuccess }
        };

        [Fact]
        public async Task GetPreferences_NoRow_ReturnsDefaults()
        {
            // Arrange
            await using TestHost host = await StartAsync();

            // Act
            HttpResponseMessage response = await host.Client.GetAsync(PreferencesUrl);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JsonElement root = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(root.GetProperty("pushEnabled").GetBoolean());
            Assert.Equal(0, root.GetProperty("minimumSeverity").GetInt32());
            Assert.False(root.GetProperty("emailEnabled").GetBoolean());
            Assert.Empty(root.GetProperty("emailEventTypes").EnumerateArray());
        }

        [Fact]
        public async Task PostPreferences_ValidBody_StoresValuesAndGetReturnsThem()
        {
            // Arrange
            await using TestHost host = await StartAsync();

            // Act
            HttpResponseMessage post = await host.Client.PostAsJsonAsync(PreferencesUrl, ValidBody());
            HttpResponseMessage get = await host.Client.GetAsync(PreferencesUrl);

            // Assert
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            Assert.True(host.Repository.HasRow(OperatorId));
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            JsonElement root = await get.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(root.GetProperty("pushEnabled").GetBoolean());
            Assert.Equal(1, root.GetProperty("minimumSeverity").GetInt32());
            Assert.True(root.GetProperty("emailEnabled").GetBoolean());
            string?[] eventTypes = root.GetProperty("emailEventTypes").EnumerateArray().Select(e => e.GetString()).ToArray();
            Assert.Equal(new[] { SecurityAuditEventTypes.AuthSuccess, SecurityAuditEventTypes.PairingCompleted }, eventTypes);
        }

        [Fact]
        public async Task PostPreferences_UnknownEventType_Returns400NamingTheValue()
        {
            // Arrange
            await using TestHost host = await StartAsync();
            var body = new
            {
                pushEnabled = true,
                minimumSeverity = 1,
                emailEnabled = true,
                emailEventTypes = new[] { SecurityAuditEventTypes.AuthSuccess, "NotARealEvent" }
            };

            // Act
            HttpResponseMessage response = await host.Client.PostAsJsonAsync(PreferencesUrl, body);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            string text = await response.Content.ReadAsStringAsync();
            Assert.Contains("NotARealEvent", text);
            Assert.False(host.Repository.HasRow(OperatorId));
        }

        [Theory]
        [InlineData(4)]
        [InlineData(-1)]
        public async Task PostPreferences_SeverityOutOfRange_Returns400(int severity)
        {
            // Arrange
            await using TestHost host = await StartAsync();
            var body = new
            {
                pushEnabled = true,
                minimumSeverity = severity,
                emailEnabled = false,
                emailEventTypes = Array.Empty<string>()
            };

            // Act
            HttpResponseMessage response = await host.Client.PostAsJsonAsync(PreferencesUrl, body);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(host.Repository.HasRow(OperatorId));
        }

        [Fact]
        public async Task GetAndPostPreferences_Anonymous_Returns401()
        {
            // Arrange
            await using TestHost host = await StartAsync();
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, PreferencesUrl);
            getRequest.Headers.Add("X-Test-Anonymous", "1");
            using var postRequest = new HttpRequestMessage(HttpMethod.Post, PreferencesUrl) { Content = JsonContent.Create(ValidBody()) };
            postRequest.Headers.Add("X-Test-Anonymous", "1");

            // Act
            HttpResponseMessage getResponse = await host.Client.SendAsync(getRequest);
            HttpResponseMessage postResponse = await host.Client.SendAsync(postRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
            Assert.False(host.Repository.HasRow(OperatorId));
        }
    }
}
