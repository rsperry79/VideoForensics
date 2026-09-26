using System.Net;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Services;

namespace VideoForensics.WebApp.Tests
{
    public class BannedIpMatchServiceTests
    {
        /// <summary>
        /// When an IP is within an active banned CIDR range, IsIpBannedAsync should return true.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_IpInActiveRange_ReturnsTrue()
        {
            // Arrange
            var testIp = IPAddress.Parse("203.0.113.50"); // IP within the 203.0.113.0/24 range

            var ranges = new List<BannedIpRange>
            {
                new BannedIpRange
                {
                    Id = Guid.NewGuid(),
                    CidrRange = "203.0.113.0/24",
                    Reason = "Test ban",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null // Permanent ban
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            var result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// When an IP is outside all banned CIDR ranges, IsIpBannedAsync should return false.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_IpOutsideAllRanges_ReturnsFalse()
        {
            // Arrange
            var testIp = IPAddress.Parse("1.1.1.1"); // IP outside the 203.0.113.0/24 range

            var ranges = new List<BannedIpRange>
            {
                new BannedIpRange
                {
                    Id = Guid.NewGuid(),
                    CidrRange = "203.0.113.0/24",
                    Reason = "Test ban",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            var result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// When no banned ranges are active, IsIpBannedAsync should return false for any IP.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_NoActiveBans_ReturnsFalse()
        {
            // Arrange
            var testIp = IPAddress.Parse("8.8.8.8");
            var ranges = new List<BannedIpRange>();

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            var result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// When a CIDR range string is malformed, IsIpBannedAsync should log/skip that range
        /// and continue checking other ranges without throwing.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_MalformedCidrRange_SkipsAndContinuesChecking()
        {
            // Arrange
            var testIp = IPAddress.Parse("203.0.113.50");

            var ranges = new List<BannedIpRange>
            {
                new BannedIpRange
                {
                    Id = Guid.NewGuid(),
                    CidrRange = "not-a-valid-cidr", // Malformed
                    Reason = "Bad range",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null
                },
                new BannedIpRange
                {
                    Id = Guid.NewGuid(),
                    CidrRange = "203.0.113.0/24", // Valid range
                    Reason = "Good range",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            var result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

            // Assert - should return true because the IP matches the valid range, despite the malformed one
            Assert.True(result);
        }

        /// <summary>
        /// IsIpBannedAsync should only check active (non-expired) ranges.
        /// Expired ranges should not affect the result.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_ExpiredRangeIgnored_ReturnsFalse()
        {
            // Arrange
            var testIp = IPAddress.Parse("203.0.113.50");
            var expiredTime = DateTime.UtcNow.AddMinutes(-10); // Expired 10 minutes ago

            var ranges = new List<BannedIpRange>
            {
                new BannedIpRange
                {
                    Id = Guid.NewGuid(),
                    CidrRange = "203.0.113.0/24",
                    Reason = "Expired ban",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = expiredTime // Already expired
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            // GetActiveAsync should only return non-expired ranges
            mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<BannedIpRange>()); // Empty because the range is expired

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            var result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

            // Assert - should return false because no active ranges exist
            Assert.False(result);
        }

        /// <summary>
        /// IsIpBannedAsync should support IPv6 CIDR ranges as well as IPv4.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_IPv6Range_ReturnsTrue()
        {
            // Arrange
            var testIp = IPAddress.Parse("2001:db8::1"); // IPv6 within the 2001:db8::/32 range

            var ranges = new List<BannedIpRange>
            {
                new BannedIpRange
                {
                    Id = Guid.NewGuid(),
                    CidrRange = "2001:db8::/32",
                    Reason = "IPv6 ban",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            var result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

            // Assert
            Assert.True(result);
        }
    }
}
