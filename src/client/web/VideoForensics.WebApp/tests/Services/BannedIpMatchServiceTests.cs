using Moq;

using System.Net;

using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
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
                new() {
                    Id = Guid.NewGuid(),
                    CidrRange = "203.0.113.0/24",
                    Reason = "Test ban",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null // Permanent ban
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            bool result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

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
                new() {
                    Id = Guid.NewGuid(),
                    CidrRange = "203.0.113.0/24",
                    Reason = "Test ban",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            bool result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

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
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            bool result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

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
                new() {
                    Id = Guid.NewGuid(),
                    CidrRange = "not-a-valid-cidr", // Malformed
                    Reason = "Bad range",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null
                },
                new() {
                    Id = Guid.NewGuid(),
                    CidrRange = "203.0.113.0/24", // Valid range
                    Reason = "Good range",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            bool result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

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
            DateTime expiredTime = DateTime.UtcNow.AddMinutes(-10); // Expired 10 minutes ago

            var ranges = new List<BannedIpRange>
            {
                new() {
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
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]); // Empty because the range is expired

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            bool result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

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
                new() {
                    Id = Guid.NewGuid(),
                    CidrRange = "2001:db8::/32",
                    Reason = "IPv6 ban",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByOperatorId = Guid.NewGuid(),
                    ExpiresAtUtc = null
                }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(ranges);

            var service = new BannedIpMatchService(mockRepository.Object);

            // Act
            bool result = await service.IsIpBannedAsync(testIp, CancellationToken.None);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// A range with no prefix length is an exact address and should match only that address.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_ExactIPv4Address_ReturnsTrueForSameAddress()
        {
            var ranges = new List<BannedIpRange>
            {
                new() { Id = Guid.NewGuid(), CidrRange = "203.0.113.50", Reason = "Exact", CreatedAtUtc = DateTime.UtcNow, CreatedByOperatorId = Guid.NewGuid(), ExpiresAtUtc = null }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ranges);
            var service = new BannedIpMatchService(mockRepository.Object);

            Assert.True(await service.IsIpBannedAsync(IPAddress.Parse("203.0.113.50"), CancellationToken.None));
            Assert.False(await service.IsIpBannedAsync(IPAddress.Parse("203.0.113.51"), CancellationToken.None));
        }

        /// <summary>
        /// An IPv4-mapped IPv6 loopback address (::ffff:127.0.0.1) is the IPv4 address 127.0.0.1 and should match an IPv4 range.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_IPv4MappedLoopback_MatchesIPv4Range()
        {
            var ranges = new List<BannedIpRange>
            {
                new() { Id = Guid.NewGuid(), CidrRange = "127.0.0.0/8", Reason = "Loopback", CreatedAtUtc = DateTime.UtcNow, CreatedByOperatorId = Guid.NewGuid(), ExpiresAtUtc = null }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ranges);
            var service = new BannedIpMatchService(mockRepository.Object);

            Assert.True(await service.IsIpBannedAsync(IPAddress.Parse("::ffff:127.0.0.1"), CancellationToken.None));
            Assert.True(await service.IsIpBannedAsync(IPAddress.Parse("127.0.0.1"), CancellationToken.None));
        }

        /// <summary>
        /// An IPv6 loopback range should not match IPv4 loopback, and an IPv6 address outside the range should not match.
        /// </summary>
        [Fact]
        public async Task IsIpBannedAsync_IPv6LoopbackAndOutsideRange_RespectsFamilyAndPrefix()
        {
            var ranges = new List<BannedIpRange>
            {
                new() { Id = Guid.NewGuid(), CidrRange = "::1/128", Reason = "IPv6 loopback", CreatedAtUtc = DateTime.UtcNow, CreatedByOperatorId = Guid.NewGuid(), ExpiresAtUtc = null },
                new() { Id = Guid.NewGuid(), CidrRange = "2001:db8::/32", Reason = "IPv6 range", CreatedAtUtc = DateTime.UtcNow, CreatedByOperatorId = Guid.NewGuid(), ExpiresAtUtc = null }
            };

            var mockRepository = new Mock<IBannedIpRangeRepository>();
            _ = mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ranges);
            var service = new BannedIpMatchService(mockRepository.Object);

            Assert.True(await service.IsIpBannedAsync(IPAddress.IPv6Loopback, CancellationToken.None));
            Assert.True(await service.IsIpBannedAsync(IPAddress.Parse("2001:db8:ffff::1"), CancellationToken.None));
            Assert.False(await service.IsIpBannedAsync(IPAddress.Parse("2001:db9::1"), CancellationToken.None));
            Assert.False(await service.IsIpBannedAsync(IPAddress.Parse("127.0.0.1"), CancellationToken.None));
        }
    }
}
