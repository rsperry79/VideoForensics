using System.Text.Json;

using Xunit;

namespace VideoForensics.Providers.Ring.Utils.Tests
{
    public class RunnerTests
    {
        [Fact]
        public void Runner_RequiresSession()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), "test");
            try
            {
                var runner = new Runner(null!, outputDir, quiet: true);
                Assert.Fail("Expected ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
                // Expected
            }
        }

        [Fact]
        public void Runner_CanBeConstructedWithValidSession()
        {
            var session = new VideoForensics.Providers.Ring.Session("user", "pass");
            string outputDir = Path.Combine(Path.GetTempPath(), "test");
            var runner = new Runner(session, outputDir, quiet: true);
            Assert.NotNull(runner);
        }

        [Fact]
        public void HttpCallRecord_HasBodyAndTimestampFields()
        {
            // Arrange
            DateTime testTime = DateTime.UtcNow;
            var record = new HttpCallRecord
            {
                Method = "GET",
                Url = "https://example.com/api",
                StatusCode = 200,
                ResponseBodyBytes = 1024,
                BodyFile = "response.json",
                Phase = "test",
                Body = "{\"key\": \"value\"}",
                TimestampUtc = testTime
            };

            // Act & Assert
            Assert.Equal("{\"key\": \"value\"}", record.Body);
            Assert.Equal(testTime, record.TimestampUtc);
        }

        [Fact]
        public void HttpCallRecord_BodyIsNotSerializedToJson()
        {
            // Arrange
            var record = new HttpCallRecord
            {
                Method = "GET",
                Url = "https://example.com/api",
                StatusCode = 200,
                ResponseBodyBytes = 1024,
                BodyFile = "response.json",
                Phase = "test",
                Body = "{\"key\": \"value\"}",
                TimestampUtc = DateTime.UtcNow
            };

            // Act
            string json = JsonSerializer.Serialize(record);

            // Assert
            Assert.NotNull(json);
            Assert.DoesNotContain("\"key\": \"value\"", json);
            Assert.DoesNotContain("\"Body\":", json);
            Assert.Contains("Method", json);
            Assert.Contains("Url", json);
        }
    }
}
