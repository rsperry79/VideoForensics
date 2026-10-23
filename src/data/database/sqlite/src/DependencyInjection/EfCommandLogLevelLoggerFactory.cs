using Microsoft.Extensions.Logging;

namespace VideoForensics.Data.Database.Sqlite.DependencyInjection
{
    /// <summary>
    /// Logs EF Core's SQL command messages at Debug. EF writes them at Information, which would print in every
    /// build, so this factory is given to the DbContext in place of the application's factory. Every other
    /// category, and EF's own warnings, pass through unchanged.
    /// </summary>
    internal sealed class EfCommandLogLevelLoggerFactory : ILoggerFactory
    {
        private const string CommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

        private readonly ILoggerFactory _inner;

        public EfCommandLogLevelLoggerFactory(ILoggerFactory inner)
        {
            _inner = inner;
        }

        public void AddProvider(ILoggerProvider provider)
        {
            _inner.AddProvider(provider);
        }

        public ILogger CreateLogger(string categoryName)
        {
            ILogger logger = _inner.CreateLogger(categoryName);
            return string.Equals(categoryName, CommandCategory, StringComparison.Ordinal) ? new LevelRemappingLogger(logger) : logger;
        }

        // The application's factory is owned by the service provider, so it is not disposed here.
        public void Dispose()
        {
        }

        private sealed class LevelRemappingLogger : ILogger
        {
            private readonly ILogger _inner;

            public LevelRemappingLogger(ILogger inner)
            {
                _inner = inner;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return _inner.BeginScope(state);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return _inner.IsEnabled(Remap(logLevel));
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _inner.Log(Remap(logLevel), eventId, state, exception, formatter);
            }

            private static LogLevel Remap(LogLevel level)
            {
                return level == LogLevel.Information ? LogLevel.Debug : level;
            }
        }
    }
}
