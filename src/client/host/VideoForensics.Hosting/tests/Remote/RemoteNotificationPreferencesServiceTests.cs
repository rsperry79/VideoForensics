using System.Net;
using System.Net.Http.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Remote;

namespace VideoForensics.Client.Hosting.Tests.Remote
{
    public class RemoteNotificationPreferencesServiceTests
    {
        [Fact]
        public async Task GetAsync_MapsJsonResponseToSummary()
        {
            // Arrange
            var dto = new VideoForensics.Core.Api.Contracts.NotificationPreferencesDto(
                PushEnabled: true,
                MinimumSeverity: 2,
                EmailEnabled: true,
                EmailEventTypes: new List<string> { "LoginAttemptFailure", "AccountLocked" }
            );

            var handler = new FakeHttpMessageHandler(
                request =>
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("/api/v1/notifications/preferences", request.RequestUri?.AbsolutePath);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(dto)
                    };
                }
            );

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteNotificationPreferencesService(httpClient);

            // Act
            NotificationPreferencesSummary result = await service.GetAsync(CancellationToken.None);

            // Assert
            Assert.True(result.PushEnabled);
            Assert.Equal(2, result.MinimumSeverity);
            Assert.True(result.EmailEnabled);
            Assert.Equal(new[] { "LoginAttemptFailure", "AccountLocked" }, result.EmailEventTypes);
        }

        [Fact]
        public async Task SaveAsync_PostsMappedBodyAndReturnsResponse()
        {
            // Arrange
            var preferences = new NotificationPreferencesSummary(
                PushEnabled: false,
                MinimumSeverity: 3,
                EmailEnabled: true,
                EmailEventTypes: new List<string> { "BreachDetected" }
            );

            string? requestBody = null;
            var handler = new FakeHttpMessageHandler(
                request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("/api/v1/notifications/preferences", request.RequestUri?.AbsolutePath);
                    requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new VideoForensics.Core.Api.Contracts.NotificationPreferencesDto(
                            PushEnabled: false,
                            MinimumSeverity: 3,
                            EmailEnabled: true,
                            EmailEventTypes: new List<string> { "BreachDetected" }))
                    };
                }
            );

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteNotificationPreferencesService(httpClient);

            // Act
            NotificationPreferencesSummary result = await service.SaveAsync(preferences, CancellationToken.None);

            // Assert
            Assert.NotNull(requestBody);
            Assert.Contains("\"minimumSeverity\":3", requestBody);
            Assert.Contains("\"emailEventTypes\":[\"BreachDetected\"]", requestBody);
            Assert.False(result.PushEnabled);
            Assert.Equal(3, result.MinimumSeverity);
            Assert.True(result.EmailEnabled);
            Assert.Equal(new[] { "BreachDetected" }, result.EmailEventTypes);
        }

        [Fact]
        public async Task GetAsync_UnauthorizedResponse_ThrowsHttpRequestException()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(
                request => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            );

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteNotificationPreferencesService(httpClient);

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(
                () => service.GetAsync(CancellationToken.None)
            );
        }

        [Fact]
        public void ImplementsINotificationPreferencesService()
        {
            var service = new RemoteNotificationPreferencesService(new HttpClient { BaseAddress = new Uri("http://localhost") });
            _ = Assert.IsAssignableFrom<INotificationPreferencesService>(service);
        }

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }
    }
}
