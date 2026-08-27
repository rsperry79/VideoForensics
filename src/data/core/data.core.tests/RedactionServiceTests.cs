using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class RedactionServiceTests
    {
        private readonly Mock<ILogger<RedactionService>> _mockLogger;
        private readonly RedactionService _service;

        public RedactionServiceTests()
        {
            _mockLogger = new Mock<ILogger<RedactionService>>();
            _service = new RedactionService(_mockLogger.Object);
        }

        [Fact]
        public void RedactForExport_WithLevelNone_ReturnsOriginalDto()
        {
            // Arrange
            var original = new TestReportDto
            {
                Email = "test@example.com",
                PhoneNumber = "555-123-4567",
                Address = "123 Main St",
                PersonName = "John Doe"
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.None);

            // Assert
            Assert.Equal("test@example.com", result.Email);
            Assert.Equal("555-123-4567", result.PhoneNumber);
            Assert.Equal("123 Main St", result.Address);
            Assert.Equal("John Doe", result.PersonName);
        }

        [Fact]
        public void RedactForExport_WithLevelLight_MasksEmailAndPhone()
        {
            // Arrange
            var original = new TestReportDto
            {
                Email = "johndoe@example.com",
                PhoneNumber = "555-123-4567",
                Address = "123 Main St",
                PersonName = "John Doe"
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.Light);

            // Assert
            // Email should be masked (first char + *** + last char @ domain)
            Assert.Contains("@example.com", result.Email);
            Assert.Contains("***", result.Email);

            // Phone should be masked (***-***-last4)
            Assert.Contains("***", result.PhoneNumber);
            Assert.EndsWith("4567", result.PhoneNumber);

            // Address and PersonName should not be masked at Light level
            Assert.Equal("123 Main St", result.Address);
            Assert.Equal("John Doe", result.PersonName);
        }

        [Fact]
        public void RedactForExport_WithLevelMedium_MasksAddressAndCoordinates()
        {
            // Arrange
            var original = new TestReportDto
            {
                Email = "test@example.com",
                Address = "456 Oak Ave",
                Latitude = "40.7128",
                Longitude = "-74.0060"
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.Medium);

            // Assert
            // Medium level includes Light (email/phone) and Medium (address/coordinates)
            Assert.Contains("***", result.Email);
            Assert.Equal("[REDACTED_ADDRESS]", result.Address);
            Assert.Equal("[REDACTED_COORDINATES]", result.Latitude);
            Assert.Equal("[REDACTED_COORDINATES]", result.Longitude);
        }

        [Fact]
        public void RedactForExport_WithLevelHeavy_MasksPersonNamesAndGps()
        {
            // Arrange
            var original = new TestReportDto
            {
                PersonName = "Jane Smith",
                GpsLocation = "Building A",
                Address = "789 Pine Ln"
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.Heavy);

            // Assert
            // Heavy level includes everything
            Assert.Equal("[REDACTED_PERSON]", result.PersonName);
            Assert.Equal("[REDACTED_GPS]", result.GpsLocation);
            Assert.Equal("[REDACTED_ADDRESS]", result.Address);
        }

        [Fact]
        public void RedactForExport_DoesNotModifyOriginal()
        {
            // Arrange
            var original = new TestReportDto
            {
                Email = "original@example.com",
                PhoneNumber = "555-999-9999",
                Address = "Original Address",
                PersonName = "Original Name"
            };

            var originalEmail = original.Email;
            var originalPhone = original.PhoneNumber;
            var originalAddress = original.Address;
            var originalName = original.PersonName;

            // Act
            var redacted = _service.RedactForExport(original, RedactionLevel.Heavy);

            // Assert - verify original is unchanged
            Assert.Equal(originalEmail, original.Email);
            Assert.Equal(originalPhone, original.PhoneNumber);
            Assert.Equal(originalAddress, original.Address);
            Assert.Equal(originalName, original.PersonName);

            // Verify redacted is different
            Assert.NotEqual(original.Email, redacted.Email);
            Assert.NotEqual(original.PhoneNumber, redacted.PhoneNumber);
            Assert.NotEqual(original.Address, redacted.Address);
            Assert.NotEqual(original.PersonName, redacted.PersonName);
        }

        [Fact]
        public void RedactForExport_WithNullEmail_HandlesGracefully()
        {
            // Arrange
            var original = new TestReportDto
            {
                Email = null,
                PhoneNumber = "555-123-4567"
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.Light);

            // Assert
            // Null should remain null or be handled
            Assert.NotNull(result.PhoneNumber);
        }

        [Fact]
        public void RedactForExport_WithShortEmail_MasksCorrectly()
        {
            // Arrange
            var original = new TestReportDto
            {
                Email = "a@example.com"
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.Light);

            // Assert
            Assert.Contains("@example.com", result.Email);
            Assert.Contains("***", result.Email);
        }

        [Fact]
        public void RedactForExport_WithShortPhoneNumber_HandlesCorrectly()
        {
            // Arrange
            var original = new TestReportDto
            {
                PhoneNumber = "12"
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.Light);

            // Assert
            // Short phone should be fully redacted
            Assert.Equal("[REDACTED_PHONE]", result.PhoneNumber);
        }

        [Fact]
        public void RedactForExport_WithNestedObjects_RedactsRecursively()
        {
            // Arrange
            var nested = new TestNestedDto
            {
                Email = "nested@example.com",
                PersonName = "Nested Person"
            };

            var original = new TestReportWithNestedDto
            {
                Email = "parent@example.com",
                NestedData = nested
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.Heavy);

            // Assert
            Assert.Contains("***", result.Email);
            Assert.NotNull(result.NestedData);
            Assert.Equal("[REDACTED_PERSON]", result.NestedData.PersonName);
        }

        [Fact]
        public void RedactForExport_MaintainsDataStructure()
        {
            // Arrange
            var original = new TestReportDto
            {
                Email = "test@example.com",
                PhoneNumber = "555-123-4567",
                Address = "123 Main St"
            };

            // Act
            var result = _service.RedactForExport(original, RedactionLevel.Medium);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Email);
            Assert.NotNull(result.PhoneNumber);
            Assert.NotNull(result.Address);
        }

        // Test DTOs
        private class TestReportDto
        {
            public string? Email { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Address { get; set; }
            public string? PersonName { get; set; }
            public string? GpsLocation { get; set; }
            public string? Latitude { get; set; }
            public string? Longitude { get; set; }
        }

        private class TestNestedDto
        {
            public string? Email { get; set; }
            public string? PersonName { get; set; }
        }

        private class TestReportWithNestedDto
        {
            public string? Email { get; set; }
            public TestNestedDto? NestedData { get; set; }
        }
    }
}
