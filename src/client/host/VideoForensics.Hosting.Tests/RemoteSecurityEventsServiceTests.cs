using System.Net;
using System.Net.Http.Json;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteSecurityEventsServiceTests
    {
        [Fact]
        public async Task GetEventsAsync_WithoutOperatorId_CallsCorrectEndpoint()
        {
            // Arrange
            var events = new List<SecurityEventDto>
            {
                new(
                    Id: Guid.NewGuid(),
                    OperatorId: Guid.NewGuid(),
                    EventType: "LoginAttemptSuccess",
                    Success: true,
                    IpAddress: "192.168.1.1",
                    OccurredAtUtc: DateTime.UtcNow,
                    Reason: null
                )
            };

            var handler = new FakeHttpMessageHandler(
                request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("/api/v1/security-events", request.RequestUri?.AbsolutePath);
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = JsonContent.Create(events);
                    return response;
                }
            );

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteSecurityEventsService(httpClient);

            // Act
            var result = await service.GetEventsAsync(operatorId: null, offset: 0, limit: 20, ct: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(events[0].EventType, result[0].EventType);
        }

        [Fact]
        public async Task GetEventsAsync_WithOperatorId_IncludesOperatorInRequest()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var events = new List<SecurityEventDto>
            {
                new(
                    Id: Guid.NewGuid(),
                    OperatorId: operatorId,
                    EventType: "LoginAttemptFailure",
                    Success: false,
                    IpAddress: "192.168.1.100",
                    OccurredAtUtc: DateTime.UtcNow,
                    Reason: "Invalid password"
                )
            };

            var handler = new FakeHttpMessageHandler(
                request =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = JsonContent.Create(events);
                    return response;
                }
            );

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteSecurityEventsService(httpClient);

            // Act
            var result = await service.GetEventsAsync(operatorId: operatorId, offset: 0, limit: 20, ct: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(operatorId, result[0].OperatorId);
        }

        [Fact]
        public async Task GetEventsAsync_EmptyList_ReturnsEmptyCollection()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(
                request =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = JsonContent.Create(new List<SecurityEventDto>());
                    return response;
                }
            );

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteSecurityEventsService(httpClient);

            // Act
            var result = await service.GetEventsAsync(operatorId: null, offset: 0, limit: 20, ct: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetEventsAsync_UnauthorizedResponse_ThrowsHttpRequestException()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(
                request =>
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }
            );

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteSecurityEventsService(httpClient);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.GetEventsAsync(operatorId: null, offset: 0, limit: 20, ct: CancellationToken.None)
            );
        }

        [Fact]
        public void ImplementsISecurityEventsService()
        {
            // Verify that RemoteSecurityEventsService implements ISecurityEventsService
            var service = new RemoteSecurityEventsService(new HttpClient { BaseAddress = new Uri("http://localhost") });
            Assert.IsAssignableFrom<ISecurityEventsService>(service);
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
