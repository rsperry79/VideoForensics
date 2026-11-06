using Microsoft.Extensions.Logging;

using System;
using System.Threading.Tasks;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>
    /// Writes the last log lines a dying process would otherwise lose. A fatal exception on a thread the host does not own
    /// (async void, a timer callback, a thread-pool item) goes only to stderr, which a service or container discards.
    /// A "process exiting" line that is missing from the log means the process was killed from outside.
    /// </summary>
    public sealed class ProcessCrashLogger
    {
        private readonly ILogger _logger;
        private readonly Action _flush;

        /// <param name="logger">Receives the crash and exit lines.</param>
        /// <param name="flush">Flushes and closes the log sinks; called only when the process is ending, since the logger is unusable afterwards.</param>
        public ProcessCrashLogger(ILogger logger, Action flush)
        {
            _logger = logger;
            _flush = flush;
        }

        /// <summary>Subscribes to the process-wide crash and exit events and logs the start line. Dispose to unsubscribe.</summary>
        public IDisposable Attach()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _logger.LogInformation("Process crash logging started (process {ProcessId})", Environment.ProcessId);
            return new Subscription(this);
        }

        /// <summary>Logs an exception from any thread and, when it ends the process, flushes.</summary>
        public void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            // Message and type only: the exception object is not passed to the logger (no stack traces in the log).
            string detail = e.ExceptionObject is Exception ex
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"non-exception object {e.ExceptionObject?.GetType().Name ?? "null"}";
            _logger.LogCritical("Unhandled exception, process terminating={IsTerminating}: {Detail}", e.IsTerminating, detail);
            _flush();
        }

        /// <summary>Logs a faulted task nobody awaited. The process keeps running, so nothing is flushed.</summary>
        public void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Exception first = e.Exception.InnerExceptions.Count > 0 ? e.Exception.InnerExceptions[0] : e.Exception;
            _logger.LogError("Unobserved task exception: {ExceptionType}: {Message}", first.GetType().Name, first.Message);
            _flush();
        }

        /// <summary>Logs the exit line and flushes.</summary>
        public void OnProcessExit(object? sender, EventArgs e)
        {
            _logger.LogInformation("Process exiting (exit code {ExitCode})", Environment.ExitCode);
            _flush();
        }

        private sealed class Subscription(ProcessCrashLogger owner) : IDisposable
        {
            public void Dispose()
            {
                AppDomain.CurrentDomain.UnhandledException -= owner.OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= owner.OnUnobservedTaskException;
                AppDomain.CurrentDomain.ProcessExit -= owner.OnProcessExit;
            }
        }
    }
}
