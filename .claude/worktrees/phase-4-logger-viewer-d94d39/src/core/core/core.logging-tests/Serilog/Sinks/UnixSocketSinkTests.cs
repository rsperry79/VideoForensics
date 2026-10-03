using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using VideoForensics.Core.Logging.Serilog.Sinks;

using Xunit;

namespace VideoForensics.Core.Logging.Tests.Serilog.Sinks
{
    public class UnixSocketSinkTests : IAsyncLifetime
    {
        private UnixSocketSink _sink = null!;
        private ILogger _logger = null!;

        public async ValueTask InitializeAsync()
        {
            _sink = new UnixSocketSink();
            _logger = new LoggerConfiguration()
                .MinimumLevel.Verbose() // Enable all log levels
                .WriteTo.Sink(_sink)
                .CreateLogger();

            await ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _sink?.Dispose();
            (_logger as IDisposable)?.Dispose();
            await ValueTask.CompletedTask;
        }

        [Fact]
        public void Emit_JsonFormatIsValid()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Act
            _logger.Error("Error test");

            // Assert - retrieve from buffer and verify JSON structure
            var buffered = _sink.GetBufferedEntries();
            Assert.NotEmpty(buffered);

            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(buffered[^1]);
            Assert.NotNull(parsed);
            Assert.True(parsed.ContainsKey("timestamp"));
            Assert.True(parsed.ContainsKey("level"));
            Assert.True(parsed.ContainsKey("message"));
            Assert.Equal("Error", parsed["level"]?.ToString());
            Assert.Contains("Error test", parsed["message"]?.ToString() ?? string.Empty);
        }

        [Fact]
        public void Emit_IncludesTimestampInIso8601Format()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Act
            _logger.Information("Test");

            // Assert
            var buffered = _sink.GetBufferedEntries();
            Assert.NotEmpty(buffered);
            var json = buffered[^1];
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            Assert.NotNull(parsed);
            Assert.True(parsed.ContainsKey("timestamp"));

            var timestampStr = parsed["timestamp"]?.ToString();
            Assert.NotNull(timestampStr);

            // Should contain the ISO 8601 format markers (T, Z or +/- offset)
            Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", timestampStr);

            // Verify it can be parsed as a DateTime
            var parsed_dt = DateTime.Parse(timestampStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.NotEqual(default(DateTime), parsed_dt);
        }

        [Fact]
        public void Emit_CircularBuffer_MaintainsMax500Entries()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            const int testEntries = 650;

            // Act - log more than 500 entries
            for (int i = 0; i < testEntries; i++)
            {
                _logger.Information($"Message {i}");
            }

            // Assert - verify buffer contains only last 500
            var buffered = _sink.GetBufferedEntries();
            Assert.Equal(500, buffered.Length);

            // Verify the first buffered entry is from ~entry 150 (650 - 500)
            Assert.Contains("Message 150", buffered[0]);

            // Verify the last buffered entry is from entry 649
            Assert.Contains("Message 649", buffered[499]);
        }

        [Fact]
        public void Emit_IncludesLogLevel()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Act
            _logger.Warning("Warning test");

            // Assert
            var buffered = _sink.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(buffered[^1]);
            Assert.NotNull(parsed);
            Assert.Equal("Warning", parsed["level"]?.ToString());
        }

        [Fact]
        public void Emit_IncludesMessage()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Act
            _logger.Information("Test message content");

            // Assert
            var buffered = _sink.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(buffered[^1]);
            Assert.NotNull(parsed);
            Assert.Contains("Test message content", parsed["message"]?.ToString() ?? string.Empty);
        }

        [Fact]
        public void Emit_WithException_IncludesExceptionInMessage()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Act
            var ex = new InvalidOperationException("Test exception");
            _logger.Error(ex, "Error with exception");

            // Assert
            var buffered = _sink.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(buffered[^1]);
            Assert.NotNull(parsed);
            Assert.Contains("Error with exception", parsed["message"]?.ToString() ?? string.Empty);
            Assert.Contains("InvalidOperationException", parsed["message"]?.ToString() ?? string.Empty);
            Assert.Contains("Test exception", parsed["message"]?.ToString() ?? string.Empty);
        }

        [Fact]
        public void Emit_ThreadSafe_ConcurrentWrites()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

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
                        _logger.Information($"Thread {threadId} Message {i}");
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert - verify all messages were logged (buffer will have last 500 if total > 500)
            var buffered = _sink.GetBufferedEntries();
            Assert.NotEmpty(buffered);
            Assert.True(buffered.Length <= 500);

            // Verify format is still valid
            foreach (var entry in buffered)
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(entry);
                Assert.NotNull(parsed);
                Assert.True(parsed.ContainsKey("timestamp"));
                Assert.True(parsed.ContainsKey("level"));
                Assert.True(parsed.ContainsKey("message"));
            }
        }

        [Fact]
        public void GetBufferedEntries_ReturnsSnapshot()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Arrange
            _logger.Information("Message 1");
            var firstSnapshot = _sink.GetBufferedEntries();

            // Act
            _logger.Information("Message 2");
            var secondSnapshot = _sink.GetBufferedEntries();

            // Assert - snapshots should be different
            Assert.Single(firstSnapshot);
            Assert.Equal(2, secondSnapshot.Length);
        }

        [Fact]
        public void Dispose_PreventsNewLogWrites()
        {
            // Act
            _sink.Dispose();
            _logger.Information("Should be ignored");

            // Assert
            var buffered = _sink.GetBufferedEntries();
            Assert.Empty(buffered);
        }

        [Fact]
        public void Emit_AllLogLevels_Supported()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Arrange
            var levels = new[]
            {
                (LogEventLevel.Verbose, "Verbose"),
                (LogEventLevel.Debug, "Debug"),
                (LogEventLevel.Information, "Information"),
                (LogEventLevel.Warning, "Warning"),
                (LogEventLevel.Error, "Error"),
                (LogEventLevel.Fatal, "Fatal"),
            };

            foreach (var (level, levelName) in levels)
            {
                // Create a fresh sink and logger for each level
                var sink = new UnixSocketSink();
                var logger = new LoggerConfiguration()
                    .MinimumLevel.Verbose() // Enable all levels
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                // Act
                logger.Write(level, $"Message at level {levelName}");

                // Assert
                var buffered = sink.GetBufferedEntries();
                Assert.NotEmpty(buffered);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(buffered[^1]);
                Assert.Equal(levelName, parsed?["level"]?.ToString());

                sink.Dispose();
                (logger as IDisposable)?.Dispose();
            }
        }

        [Fact]
        public void Emit_NonLinux_IsNoOp()
        {
            if (OperatingSystem.IsLinux())
            {
                // This test is for non-Linux; skip on Linux
                return;
            }

            // Act
            _logger.Information("Test on non-Linux");

            // Assert - should not crash, buffer should be empty (no-op)
            var buffered = _sink.GetBufferedEntries();
            Assert.Empty(buffered);
        }

        [Fact]
        public void Emit_WithProperty_IncludesInRenderedMessage()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Act
            _logger.Information("User {UserId} logged in", 42);

            // Assert
            var buffered = _sink.GetBufferedEntries();
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(buffered[^1]);
            Assert.NotNull(parsed);
            // Serilog renders properties into the message
            Assert.Contains("42", parsed["message"]?.ToString() ?? string.Empty);
        }

        [Fact]
        public void Emit_JsonEscaping_HandlesSpecialCharacters()
        {
            if (!OperatingSystem.IsLinux())
            {
                // Unix socket sink is no-op on non-Linux; skip this test
                return;
            }

            // Act
            _logger.Information("Message with \"quotes\" and \\backslashes\\ and\nnewlines");

            // Assert - should properly escape JSON
            var buffered = _sink.GetBufferedEntries();
            var json = buffered[^1];

            // Should be valid JSON
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            Assert.NotNull(parsed);
            Assert.Contains("quotes", parsed["message"]?.ToString() ?? string.Empty);
        }
    }
}
