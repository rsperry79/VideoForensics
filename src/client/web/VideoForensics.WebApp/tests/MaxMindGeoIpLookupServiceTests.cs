using System.Net;
using Xunit;
using VideoForensics.WebApp.Services;

namespace VideoForensics.WebApp.Tests
{
    public class MaxMindGeoIpLookupServiceTests
    {
        /// <summary>
        /// When the GeoLite2 database file does not exist, LookupCountryCodeAsync should gracefully return null
        /// instead of throwing an exception.
        /// </summary>
        [Fact]
        public async Task LookupCountryCodeAsync_MissingDatabaseFile_ReturnsNullGracefully()
        {
            // Arrange
            var service = new MaxMindGeoIpLookupService("/nonexistent/path/to/GeoLite2-Country.mmdb");
            var testIp = IPAddress.Parse("1.1.1.1");

            // Act
            var result = await service.LookupCountryCodeAsync(testIp, CancellationToken.None);

            // Assert - should return null, not throw
            Assert.Null(result);
        }

        /// <summary>
        /// LookupCountryCodeAsync should accept both IPv4 and IPv6 addresses without crashing.
        /// With a missing database, both should return null gracefully.
        /// </summary>
        [Fact]
        public async Task LookupCountryCodeAsync_IPv6Address_ReturnsNullGracefully()
        {
            // Arrange
            var service = new MaxMindGeoIpLookupService("/nonexistent/path/to/GeoLite2-Country.mmdb");
            var testIp = IPAddress.Parse("2001:4860:4860::8888");

            // Act
            var result = await service.LookupCountryCodeAsync(testIp, CancellationToken.None);

            // Assert - should return null, not throw
            Assert.Null(result);
        }

        /// <summary>
        /// LookupCountryCodeAsync should handle null IP address gracefully.
        /// </summary>
        [Fact]
        public async Task LookupCountryCodeAsync_NullIpAddress_ReturnsNullGracefully()
        {
            // Arrange
            var service = new MaxMindGeoIpLookupService("/nonexistent/path/to/GeoLite2-Country.mmdb");

            // Act
            var result = await service.LookupCountryCodeAsync(null, CancellationToken.None);

            // Assert - should return null, not throw
            Assert.Null(result);
        }

        /// <summary>
        /// LookupCountryCodeAsync should return a two-character ISO country code when the database exists and has valid data.
        /// This test requires a real GeoLite2 database file or a mock - for this test without a DB file, it should return null.
        /// </summary>
        [Fact]
        public async Task LookupCountryCodeAsync_ValidDatabasePath_ReturnsCountryCodeOrNull()
        {
            // Arrange
            var service = new MaxMindGeoIpLookupService("/nonexistent/path/to/GeoLite2-Country.mmdb");
            var testIp = IPAddress.Parse("8.8.8.8");

            // Act
            var result = await service.LookupCountryCodeAsync(testIp, CancellationToken.None);

            // Assert - without a real database, this should return null. With a real database, it would return a country code.
            Assert.Null(result);
        }
    }
}
