using System.Net;
using System.Net.Http.Json;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Remote;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteAdminOperatorServiceTests
    {
        [Fact]
        public void UnlockAsync_MethodExists()
        {
            // Verify that RemoteAdminOperatorService has the UnlockAsync method
            var service = new RemoteAdminOperatorService(new HttpClient() { BaseAddress = new Uri("http://localhost") });

            // Verify the method exists and is accessible
            var method = typeof(RemoteAdminOperatorService).GetMethod("UnlockAsync");
            Assert.NotNull(method);
            Assert.True(method?.IsPublic);
        }

        [Fact]
        public void ImplementsIAdminOperatorService()
        {
            // Verify that RemoteAdminOperatorService implements IAdminOperatorService
            var service = new RemoteAdminOperatorService(new HttpClient() { BaseAddress = new Uri("http://localhost") });
            Assert.IsAssignableFrom<IAdminOperatorService>(service);
        }

        [Fact]
        public void HasCorrectHttpClientDependency()
        {
            // Verify that the service accepts an HttpClient in its constructor
            var httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);
            Assert.NotNull(service);
        }

        [Fact]
        public async Task ListOperatorsAsync_CallsExpectedEndpoint_ReturnsMappedSummaries()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var summaries = new List<OperatorSummaryDto>
            {
                new(operatorId, "Jane Operator", true)
            };

            var handler = new FakeHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("/api/devices-management/operators", request.RequestUri?.AbsolutePath);
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = JsonContent.Create(summaries);
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);

            // Act
            var result = await service.ListOperatorsAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(operatorId, result[0].Id);
            Assert.Equal("Jane Operator", result[0].DisplayName);
            Assert.True(result[0].Active);
        }

        [Fact]
        public async Task ListOperatorsAsync_EmptyList_ReturnsEmptyCollection()
        {
            var handler = new FakeHttpMessageHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = JsonContent.Create(new List<OperatorSummaryDto>());
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);

            var result = await service.ListOperatorsAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ListOperatorsAsync_ForbiddenResponse_ThrowsHttpRequestException()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.ListOperatorsAsync(CancellationToken.None));
        }

        
        [Fact]
        public async Task GetUiModeAsync_CallsExpectedEndpoint_ReturnsMappedValues()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var dto = new OperatorUiModeDto("Simple", true);

            var handler = new FakeHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal($"/api/devices-management/operators/{operatorId}/ui-mode", request.RequestUri?.AbsolutePath);
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = JsonContent.Create(dto);
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);

            // Act
            var result = await service.GetUiModeAsync(operatorId, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Simple", result.Mode);
            Assert.True(result.Locked);
        }

        [Fact]
        public async Task GetUiModeAsync_NotFoundResponse_ThrowsHttpRequestException()
        {
            var operatorId = Guid.NewGuid();
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.GetUiModeAsync(operatorId, CancellationToken.None));
        }

        [Fact]
        public async Task SetUiModeAsync_CallsExpectedEndpoint_WithCorrectMethod()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var mode = "Standard";
            var locked = false;

            var handler = new FakeHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal($"/api/devices-management/operators/{operatorId}/ui-mode", request.RequestUri?.AbsolutePath);
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = JsonContent.Create(new OperatorUiModeDto(mode, locked));
                return response;
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);

            // Act & Assert - should not throw
            await service.SetUiModeAsync(operatorId, mode, locked, CancellationToken.None);
        }

        [Fact]
        public async Task SetUiModeAsync_BadRequestResponse_ThrowsHttpRequestException()
        {
            var operatorId = Guid.NewGuid();
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.SetUiModeAsync(operatorId, "Invalid", false, CancellationToken.None));
        }

        [Fact]
        public async Task SetUiModeAsync_NotFoundResponse_ThrowsHttpRequestException()
        {
            var operatorId = Guid.NewGuid();
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var service = new RemoteAdminOperatorService(httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(
                () => service.SetUiModeAsync(operatorId, "Simple", true, CancellationToken.None));
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
