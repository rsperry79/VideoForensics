using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace VideoForensics.Data.Database.Sqlite.DependencyInjection
{
    /// <summary>
    /// Logs EF Core's chatty messages at Trace: every message in the Database.Command category (SQL commands and their
    /// parameters, written at Debug or Information), and the "started tracking" event in the ChangeTracking category
    /// (written at Debug). Matched by EF's own category and event types, so the level names what the message is.
    /// This factory is given to the DbContext in place of the application's factory. Warnings and errors, and every
    /// other event, pass through unchanged.
    /// </summary>
    internal sealed class EfCommandLogLevelLoggerFactory : ILoggerFactory
    {
        private static readonly string CommandCategory = DbLoggerCategory.Database.Command.Name;
        private static readonly string ChangeTrackingCategory = DbLoggerCategory.ChangeTracking.Name;

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
            if (string.Equals(categoryName, CommandCategory, StringComparison.Ordinal))
            {
                return new EventRemappingLogger(logger, _ => true);
            }

            if (string.Equals(categoryName, ChangeTrackingCategory, StringComparison.Ordinal))
            {
                return new EventRemappingLogger(logger, eventId => eventId.Id == CoreEventId.StartedTracking.Id);
            }

            return logger;
        }

        // The application's factory is owned by the service provider, so it is not disposed here.
        public void Dispose()
        {
        }

        private sealed class EventRemappingLogger(ILogger inner, Func<EventId, bool> isTraceEvent) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return inner.BeginScope(state);
            }

            // Not remapped here: the event id is only known in Log. A Trace event that the inner logger filters out is dropped there.
            public bool IsEnabled(LogLevel logLevel)
            {
                return inner.IsEnabled(logLevel);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                bool chatty = logLevel is LogLevel.Debug or LogLevel.Information && isTraceEvent(eventId);
                inner.Log(chatty ? LogLevel.Trace : logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
