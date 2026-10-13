using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Api;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class DeviceManagementEndpointsGetTests
    {
        [Fact]
        public async Task GetOperator_ExistingId_ReturnsOperatorWithoutPasswordHash()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator
                {
                    Id = operatorId,
                    DisplayName = "Jane Operator",
                    Username = "jane",
                    FirstName = "Jane",
                    LastName = "Operator",
                    Email = "jane@example.com",
                    PasswordHash = "AQAAAA-secret-hash",
                    SecurityStamp = Guid.NewGuid(),
                });

            // Act
            IResult result = await DeviceManagementEndpoints.GetOperatorAsync(operatorId, operators.Object, CancellationToken.None);

            // Assert
            IValueHttpResult valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
            OperatorDetailsDto dto = Assert.IsType<OperatorDetailsDto>(valueResult.Value);
            Assert.Equal(operatorId, dto.Id);
            Assert.Equal("Jane Operator", dto.DisplayName);

            string json = JsonSerializer.Serialize(dto);
            Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AQAAAA-secret-hash", json);
        }

        [Fact]
        public async Task GetOperator_UnknownId_ReturnsNotFound()
        {
            // Arrange
            var operators = new Mock<IOperatorRepository>();
            operators.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Operator?)null);

            // Act
            IResult result = await DeviceManagementEndpoints.GetOperatorAsync(Guid.NewGuid(), operators.Object, CancellationToken.None);

            // Assert
            IStatusCodeHttpResult status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, status.StatusCode);
        }
    }
}
