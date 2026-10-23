using Moq.Protected;

using System.Net;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Remote;

namespace VideoForensics.Client.Hosting.Tests
{
    public class RemoteTwoFactorPolicyServiceTests
    {
        private static HttpClient CreateMockHttpClient(HttpResponseMessage responseMessage)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            _ = handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseMessage);

            return new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:5162")
            };
        }

        [Fact]
        public async Task GetRoleRequirementsAsync_ReturnsRoleRequirements()
        {
            // Arrange
            var dtos = new List<TwoFactorRoleRequirementDto>
            {
                new(OperatorRole.ReadOnly, true, DateTime.UtcNow),
                new(OperatorRole.Review, true, DateTime.UtcNow),
                new(OperatorRole.Admin, true, DateTime.UtcNow),
                new(OperatorRole.SuperAdmin, true, DateTime.UtcNow)
            };

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(dtos), System.Text.Encoding.UTF8, "application/json")
            };

            HttpClient httpClient = CreateMockHttpClient(response);
            var service = new RemoteTwoFactorPolicyService(httpClient);

            // Act
            IReadOnlyList<TwoFactorRoleRequirement> result = await service.GetRoleRequirementsAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(4, result.Count);
            Assert.All(result, r => Assert.True(r.RequireTwoFactor));
        }

        [Fact]
        public async Task UpdateRoleRequirementAsync_CallsCorrectEndpoint()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            var handlerMock = new Mock<HttpMessageHandler>();
            _ = handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(m =>
                        m.Method == HttpMethod.Put &&
                        m.RequestUri.ToString().Contains("/api/v1/two-factor-policy/roles/ReadOnly")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:5162")
            };

            var service = new RemoteTwoFactorPolicyService(httpClient);

            // Act
            await service.UpdateRoleRequirementAsync(OperatorRole.ReadOnly, false, CancellationToken.None);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Put &&
                    m.RequestUri.ToString().Contains("/api/v1/two-factor-policy/roles/ReadOnly")),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task UpdateOperatorOverrideAsync_CallsCorrectEndpoint()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            var handlerMock = new Mock<HttpMessageHandler>();
            _ = handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(m =>
                        m.Method == HttpMethod.Put &&
                        m.RequestUri.ToString().Contains($"/api/v1/two-factor-policy/operators/{operatorId}/override")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:5162")
            };

            var service = new RemoteTwoFactorPolicyService(httpClient);

            // Act
            await service.UpdateOperatorOverrideAsync(operatorId, TwoFactorRequirementOverride.Required, CancellationToken.None);

            // Assert
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Put &&
                    m.RequestUri.ToString().Contains($"/api/v1/two-factor-policy/operators/{operatorId}/override")),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetRoleRequirementsAsync_WithErrorResponse_Throws()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            HttpClient httpClient = CreateMockHttpClient(response);
            var service = new RemoteTwoFactorPolicyService(httpClient);

            // Act & Assert
            _ = await Assert.ThrowsAsync<HttpRequestException>(() =>
                service.GetRoleRequirementsAsync(CancellationToken.None));
        }
    }
}
