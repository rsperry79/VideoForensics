using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Core.Logging.Providers;
using VideoForensics.Core.Logging.Services;

using Xunit;

namespace VideoForensics.Core.Logging.Tests
{
    public class InMemoryLogBufferProviderTests : IAsyncLifetime
    {
        private InMemoryLogBuffer _buffer = null!;
        private InMemoryLogBufferProvider _provider = null!;
        private ILogger _logger = null!;

        public async ValueTask InitializeAsync()
        {
            _buffer = new InMemoryLogBuffer(capacity: 5000);
            _provider = new InMemoryLogBufferProvider(_buffer);
            _logger = _provider.CreateLogger("TestCategory");
            await ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _provider?.Dispose();
            _buffer?.Dispose();
            await ValueTask.CompletedTask;
        }

        [Fact]
        public void LogInformation_CapturesLevel()
        {
            // Act
            _logger.LogInformation("Test message");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Equal("Information", snapshot[0].Level);
        }

        [Fact]
        public void LogError_CapturesLevel()
        {
            // Act
            _logger.LogError("Error message");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Equal("Error", snapshot[0].Level);
        }

        [Fact]
        public void LogInformation_CapturesCategory()
        {
            // Act
            _logger.LogInformation("Test");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Equal("TestCategory", snapshot[0].Category);
        }

        [Fact]
        public void LogInformation_CapturesMessage()
        {
            // Act
            var testMessage = "This is a test log message";
            _logger.LogInformation(testMessage);

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Contains(testMessage, snapshot[0].Message);
        }

        [Fact]
        public void LogError_WithException_CapturesExceptionText()
        {
            // Act
            var ex = new InvalidOperationException("Test exception");
            _logger.LogError(ex, "Error occurred");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.NotNull(snapshot[0].Exception);
            Assert.Contains("InvalidOperationException", snapshot[0].Exception);
            Assert.Contains("Test exception", snapshot[0].Exception);
        }

        [Fact]
        public void LogError_WithException_IncludesStackTrace()
        {
            // Act
            Exception ex;
            try
            {
                throw new NullReferenceException("Value is null");
            }
            catch (Exception e)
            {
                ex = e;
            }

            _logger.LogError(ex, "Caught exception");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.NotNull(snapshot[0].Exception);
            Assert.Contains("NullReferenceException", snapshot[0].Exception);
            Assert.Contains("Value is null", snapshot[0].Exception);
        }

        [Fact]
        public void LogInformation_WithException_NullIsHandled()
        {
            // Act
            _logger.LogInformation("No exception");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Null(snapshot[0].Exception);
        }

        [Fact]
        public void LogError_WithException_TruncatedTo4000Chars()
        {
            // Act
            var longMessage = new string('x', 5000);
            var ex = new Exception(longMessage);
            _logger.LogError(ex, "Error");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.NotNull(snapshot[0].Exception);
            Assert.True(snapshot[0].Exception.Length <= 4000);
        }

        [Fact]
        public void LogInformation_CapturesTimestamp()
        {
            // Act
            var beforeLog = DateTimeOffset.UtcNow;
            _logger.LogInformation("Test");
            var afterLog = DateTimeOffset.UtcNow;

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.True(snapshot[0].TimestampUtc >= beforeLog);
            Assert.True(snapshot[0].TimestampUtc <= afterLog);
        }

        [Fact]
        public void LogInformation_MultipleMessages_AllCaptured()
        {
            // Act
            _logger.LogInformation("Message 1");
            _logger.LogInformation("Message 2");
            _logger.LogInformation("Message 3");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 100);
            Assert.Equal(3, snapshot.Count);
        }

        [Theory]
        [InlineData(LogLevel.Trace)]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Information)]
        [InlineData(LogLevel.Warning)]
        [InlineData(LogLevel.Error)]
        [InlineData(LogLevel.Critical)]
        public void Log_AllLogLevels_Captured(LogLevel level)
        {
            // Act
            _logger.Log(level, "Message at " + level.ToString());

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Equal(level.ToString(), snapshot[0].Level);
        }

        [Fact]
        public void LogWithMessage_Redacted_BearerToken()
        {
            // Act
            _logger.LogInformation("Request with Bearer sk-token-123");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Contains("[REDACTED]", snapshot[0].Message);
            Assert.DoesNotContain("sk-token-123", snapshot[0].Message);
        }

        [Fact]
        public void LogWithPassword_Redacted()
        {
            // Act
            _logger.LogInformation("Connect with password=MySecret");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 10);
            Assert.Single(snapshot);
            Assert.Contains("password=[REDACTED]", snapshot[0].Message);
            Assert.DoesNotContain("MySecret", snapshot[0].Message);
        }

        [Fact]
        public void CreateLogger_DifferentCategories()
        {
            // Act
            var logger1 = _provider.CreateLogger("Category1");
            var logger2 = _provider.CreateLogger("Category2");

            logger1.LogInformation("From Category1");
            logger2.LogInformation("From Category2");

            // Assert
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 100);
            Assert.Equal(2, snapshot.Count);
            Assert.Equal("Category1", snapshot[0].Category);
            Assert.Equal("Category2", snapshot[1].Category);
        }

        [Fact]
        public void Dispose_PreventsFutureLogging()
        {
            // Act
            _logger.LogInformation("Before dispose");
            _provider.Dispose();

            // Create a new logger from the disposed provider
            var newLogger = _provider.CreateLogger("NewCategory");
            newLogger.LogInformation("After dispose");

            // Assert - buffer should still have first entry since it's independent of provider
            var snapshot = _buffer.GetSnapshot(minLevel: null, search: null, afterSequence: null, limit: 100);
            Assert.Single(snapshot);
            Assert.Equal("Before dispose", snapshot[0].Message);
        }
    }
}
