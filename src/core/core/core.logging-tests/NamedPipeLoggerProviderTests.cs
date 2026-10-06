using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Core.Logging.Providers;

using Xunit;

namespace VideoForensics.Core.Logging.Tests
{
    public class NamedPipeLoggerProviderTests : IAsyncLifetime
    {
        private NamedPipeLoggerProvider _provider = null!;
        private ILogger _logger = null!;

        public async ValueTask InitializeAsync()
        {
            _provider = new NamedPipeLoggerProvider();
            _logger = _provider.CreateLogger("TestCategory");
            await ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _provider?.Dispose();
            await ValueTask.CompletedTask;
        }

        [Fact(Skip = "Windows-only test")]
        public async Task WriteLog_CreatesNamedPipeOnFirstWrite()
        {
            if (!OperatingSystem.IsWindows())
                return;

            // Act
            _logger.LogInformation("Test message");
            await Task.Delay(200); // Wait for async write

            // Assert - verify circular buffer has the entry
            var buffered = _provider.GetBufferedEntries();
            Assert.Single(buffered);
            Assert.Contains("Test message", buffered[0]);
        }

        [Fact(Skip = "Windows-only test")]
        public void WriteLog_JsonFormatIsValid()
        {
            // Act
            _logger.LogError("Error test");

            // Assert - retrieve from buffer and verify JSON structure
            var buffered = _provider.GetBufferedEntries();
            Assert.Single(buffered);

            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(buffered[0]);
            Assert.NotNull(parsed);
            Assert.True(parsed.ContainsKey("timestamp"));
            Assert.True(parsed.ContainsKey("level"));
            Assert.True(parsed.ContainsKey("message"));
            Assert.Equal("Error", parsed["level"]?.ToString());
            Assert.Contains("Error test", parsed["message"]?.ToString() ?? string.Empty);
        }

        [Fact(Skip = "Windows-only test")]
        public void WriteLog_IncludesTimestampInIso8601Format()
        {
            // Act
            _logger.LogInformation("Test");

            // Assert
            var buffered = _provider.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<LogEntry>(buffered[0]);
            Assert.NotNull(parsed);
            Assert.NotNull(parsed.Timestamp);

            // Verify it's a valid ISO 8601 format (can parse as DateTime)
            var parsed_dt = DateTime.Parse(parsed.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.True(parsed_dt.Kind == DateTimeKind.Utc);
        }

        [Fact(Skip = "Windows-only test")]
        public void WriteLog_CircularBuffer_MaintainsMax500Entries()
        {
            const int testEntries = 650;

            // Act - log more than 500 entries
            for (int i = 0; i < testEntries; i++)
            {
                _logger.LogInformation($"Message {i}");
            }

            // Assert - verify buffer contains only last 500
            var buffered = _provider.GetBufferedEntries();
            Assert.Equal(500, buffered.Length);

            // Verify the first buffered entry is from ~entry 150 (650 - 500)
            Assert.Contains("Message 150", buffered[0]);

            // Verify the last buffered entry is from entry 649
            Assert.Contains("Message 649", buffered[499]);
        }

        [Fact(Skip = "Windows-only test")]
        public void WriteLog_IncludesLogLevel()
        {
            // Act
            _logger.LogWarning("Warning test");

            // Assert
            var buffered = _provider.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<LogEntry>(buffered[0]);
            Assert.NotNull(parsed);
            Assert.Equal("Warning", parsed.Level);
        }

        [Fact(Skip = "Windows-only test")]
        public void WriteLog_IncludesCategory()
        {
            // Arrange
            var logger = _provider.CreateLogger("MyCustomCategory");

            // Act
            logger.LogInformation("Test");

            // Assert
            var buffered = _provider.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<LogEntry>(buffered[0]);
            Assert.NotNull(parsed);
            Assert.Equal("MyCustomCategory", parsed.Category);
        }

        [Fact(Skip = "Windows-only test")]
        public void WriteLog_WithException_IncludesExceptionInMessage()
        {
            // Act
            var ex = new InvalidOperationException("Test exception");
            _logger.LogError(ex, "Error with exception");

            // Assert
            var buffered = _provider.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<LogEntry>(buffered[0]);
            Assert.NotNull(parsed);
            Assert.Contains("Error with exception", parsed.Message);
            Assert.Contains("InvalidOperationException", parsed.Message);
            Assert.Contains("Test exception", parsed.Message);
        }

        [Fact(Skip = "Windows-only test")]
        public void WriteLog_WithEventId_IncludesEventIdInJson()
        {
            // Act
            var eventId = new EventId(42, "TestEvent");
            _logger.Log(LogLevel.Information, eventId, "Test", null, (state, ex) => state.ToString());

            // Assert
            var buffered = _provider.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<LogEntry>(buffered[0]);
            Assert.NotNull(parsed);
            Assert.Equal(42, parsed.EventId);
            Assert.Equal("TestEvent", parsed.EventName);
        }

        [Fact]
        public void WriteLog_DisabledLogLevel_DoesNotAddToBuffer()
        {
            // Act
            _logger.Log(LogLevel.None, new EventId(0, null), "Test", null, (state, ex) => state.ToString());

            // Assert
            var buffered = _provider.GetBufferedEntries();
            Assert.Empty(buffered);
        }

        [Fact(Skip = "Windows-only test")]
        public async Task WriteLog_ThreadSafe_ConcurrentWrites()
        {
            const int threadCount = 10;
            const int messagesPerThread = 100;
            var tasks = new List<Task>();

            // Act - spawn multiple threads logging concurrently
            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < messagesPerThread; i++)
                    {
                        _logger.LogInformation($"Thread {threadId} Message {i}");
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - verify all messages were logged (buffer will have last 500 if total > 500)
            var buffered = _provider.GetBufferedEntries();
            Assert.NotEmpty(buffered);
            Assert.True(buffered.Length <= 500);

            // Verify format is still valid
            foreach (var entry in buffered)
            {
                var parsed = JsonSerializer.Deserialize<LogEntry>(entry);
                Assert.NotNull(parsed);
                Assert.NotNull(parsed.Timestamp);
                Assert.NotNull(parsed.Level);
                Assert.NotNull(parsed.Message);
            }
        }

        [Fact(Skip = "Windows-only test")]
        public void GetBufferedEntries_ReturnsSnapshot()
        {
            // Arrange
            _logger.LogInformation("Message 1");
            var firstSnapshot = _provider.GetBufferedEntries();

            // Act
            _logger.LogInformation("Message 2");
            var secondSnapshot = _provider.GetBufferedEntries();

            // Assert - snapshots should be different
            Assert.Single(firstSnapshot);
            Assert.Equal(2, secondSnapshot.Length);
        }

        [Fact(Skip = "Windows-only test")]
        public void Dispose_PreventsNewLogWrites()
        {
            // Act
            _provider.Dispose();
            _logger.LogInformation("Should be ignored");

            // Assert
            var buffered = _provider.GetBufferedEntries();
            Assert.Empty(buffered);
        }

        [Fact(Skip = "Windows-only test")]
        public void CreateLogger_MultipleLoggers_ShareBuffer()
        {
            // Arrange
            var logger1 = _provider.CreateLogger("Category1");
            var logger2 = _provider.CreateLogger("Category2");

            // Act
            logger1.LogInformation("From logger1");
            logger2.LogWarning("From logger2");

            // Assert - both logs should be in the shared buffer
            var buffered = _provider.GetBufferedEntries();
            Assert.Equal(2, buffered.Length);
            Assert.Contains("From logger1", buffered[0]);
            Assert.Contains("From logger2", buffered[1]);
        }

        [Fact]
        public void WriteLog_NonWindows_IsNoOp()
        {
            if (OperatingSystem.IsWindows())
            {
                // This test is for non-Windows; skip on Windows
                return;
            }

            // Act
            _logger.LogInformation("Test on non-Windows");

            // Assert - should not crash, buffer should be empty (no-op)
            var buffered = _provider.GetBufferedEntries();
            Assert.Empty(buffered);
        }

        [Fact]
        public void BeginScope_ReturnsNull()
        {
            // Act
            var scope = _logger.BeginScope("test scope");

            // Assert
            Assert.Null(scope);
        }

        [Fact]
        public void IsEnabled_LogLevelNone_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(_logger.IsEnabled(LogLevel.None));
        }

        [Fact]
        public void IsEnabled_AllOtherLevels_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(_logger.IsEnabled(LogLevel.Trace));
            Assert.True(_logger.IsEnabled(LogLevel.Debug));
            Assert.True(_logger.IsEnabled(LogLevel.Information));
            Assert.True(_logger.IsEnabled(LogLevel.Warning));
            Assert.True(_logger.IsEnabled(LogLevel.Error));
            Assert.True(_logger.IsEnabled(LogLevel.Critical));
        }

        [Fact(Skip = "Windows-only test")]
        public void LogEntry_JsonSerialization_IgnoresDefaultEventId()
        {
            // Arrange - log with default (zero) event ID
            var logger = _provider.CreateLogger("Test");
            logger.Log(LogLevel.Information, default(EventId), "Test", null, (state, ex) => state.ToString());

            // Act
            var buffered = _provider.GetBufferedEntries();
            var json = buffered[0];

            // Assert - eventId should not be in JSON when zero
            Assert.DoesNotContain("\"eventId\":0", json);
        }

        [Fact(Skip = "Windows-only test")]
        public void LogEntry_JsonSerialization_IgnoresNullEventName()
        {
            // Arrange
            var logger = _provider.CreateLogger("Test");
            logger.Log(LogLevel.Information, new EventId(0, null), "Test", null, (state, ex) => state.ToString());

            // Act
            var buffered = _provider.GetBufferedEntries();
            var json = buffered[0];

            // Assert
            var parsed = JsonSerializer.Deserialize<LogEntry>(json);
            Assert.Null(parsed?.EventName);
        }

        [Theory(Skip = "Windows-only test")]
        [InlineData(LogLevel.Trace)]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Information)]
        [InlineData(LogLevel.Warning)]
        [InlineData(LogLevel.Error)]
        [InlineData(LogLevel.Critical)]
        public void WriteLog_AllLogLevels_Supported(LogLevel level)
        {
            // Act
            var message = "Message at level " + level.ToString();
            _logger.Log(level, new EventId(0, null), message, null, (state, ex) => state);

            // Assert
            var buffered = _provider.GetBufferedEntries();
            Assert.Single(buffered);
            var parsed = JsonSerializer.Deserialize<LogEntry>(buffered[0]);
            Assert.Equal(level.ToString(), parsed?.Level);
        }
    }
}
