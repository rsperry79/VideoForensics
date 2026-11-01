using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using VideoForensics.Core.Logging.Services;

using Xunit;

namespace VideoForensics.Core.Logging.Tests.Services
{
    public class ProcessCrashLoggerTests
    {
        private sealed class CapturingLogger : ILogger
        {
            public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception), exception));
        }

        [Fact]
        public void UnhandledException_LogsCriticalWithTypeAndMessage_NoExceptionObject_AndFlushes()
        {
            CapturingLogger logger = new();
            int flushes = 0;
            ProcessCrashLogger sut = new(logger, () => flushes++);

            sut.OnUnhandledException(this, new UnhandledExceptionEventArgs(new InvalidOperationException("boom"), isTerminating: true));

            (LogLevel Level, string Message, Exception? Exception) entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Critical, entry.Level);
            Assert.Contains("InvalidOperationException", entry.Message);
            Assert.Contains("boom", entry.Message);
            Assert.Contains("terminating", entry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(entry.Exception);
            Assert.Equal(1, flushes);
        }

        [Fact]
        public void UnhandledException_NonExceptionObject_StillLogsAndFlushes()
        {
            CapturingLogger logger = new();
            int flushes = 0;
            ProcessCrashLogger sut = new(logger, () => flushes++);

            sut.OnUnhandledException(this, new UnhandledExceptionEventArgs("not an exception", isTerminating: true));

            Assert.Equal(LogLevel.Critical, Assert.Single(logger.Entries).Level);
            Assert.Equal(1, flushes);
        }

        [Fact]
        public void UnobservedTaskException_LogsErrorWithInnerTypeAndMessage()
        {
            CapturingLogger logger = new();
            ProcessCrashLogger sut = new(logger, () => { });
            AggregateException aggregate = new(new TimeoutException("slow"));

            sut.OnUnobservedTaskException(this, new UnobservedTaskExceptionEventArgs(aggregate));

            (LogLevel Level, string Message, Exception? Exception) entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Contains("TimeoutException", entry.Message);
            Assert.Contains("slow", entry.Message);
            Assert.Null(entry.Exception);
        }

        [Fact]
        public void ProcessExit_LogsInformationAndFlushes()
        {
            CapturingLogger logger = new();
            int flushes = 0;
            ProcessCrashLogger sut = new(logger, () => flushes++);

            sut.OnProcessExit(this, EventArgs.Empty);

            Assert.Equal(LogLevel.Information, Assert.Single(logger.Entries).Level);
            Assert.Equal(1, flushes);
        }

        [Fact]
        public void Attach_SubscribesAndDisposeUnsubscribes()
        {
            CapturingLogger logger = new();
            ProcessCrashLogger sut = new(logger, () => { });

            IDisposable subscription = sut.Attach();
            subscription.Dispose();

            Assert.Single(logger.Entries);
            Assert.Contains("started", logger.Entries[0].Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
