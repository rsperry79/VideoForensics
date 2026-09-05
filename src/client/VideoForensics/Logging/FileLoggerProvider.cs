using Microsoft.Extensions.Logging;

namespace VideoForensics.Logging
{
    /// <summary>
    /// Minimal file-backed <see cref="ILoggerProvider"/>. The app previously registered logging
    /// with no provider at all (not even console), so every LogInformation/LogWarning/LogError
    /// call across the codebase - including the Ring API diagnostics used to troubleshoot device
    /// discovery - was silently discarded. This gives them somewhere to land without disrupting
    /// the interactive Spectre.Console UI the way console logging would.
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private readonly LogLevel _minLevel;
        private readonly object _writeLock = new();

        public FileLoggerProvider(string filePath, LogLevel minLevel)
        {
            _filePath = filePath;
            _minLevel = minLevel;
            _ = Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _filePath, _minLevel, _writeLock);
        }

        public void Dispose()
        {
        }

        private sealed class FileLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly string _filePath;
            private readonly LogLevel _minLevel;
            private readonly object _writeLock;

            public FileLogger(string categoryName, string filePath, LogLevel minLevel, object writeLock)
            {
                _categoryName = categoryName;
                _filePath = filePath;
                _minLevel = minLevel;
                _writeLock = writeLock;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None && logLevel >= _minLevel;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_categoryName}: {formatter(state, exception)}";
                if (exception != null)
                {
                    line += Environment.NewLine + exception;
                }

                lock (_writeLock)
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine);
                }
            }
        }
    }
}
