using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>The main database model must build without EF model warnings.</summary>
    public class MainModelWarningsTests
    {
        private const string ModelCategory = "Microsoft.EntityFrameworkCore.Model";

        [Fact]
        public void VideoForensicsDbContext_BuildsModel_WithoutModelWarnings()
        {
            // Arrange: logs-only entity types that are mapped and then ignored make EF log a model warning at startup.
            var warnings = new ConcurrentQueue<string>();
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new WarningCollector(ModelCategory, warnings)));
            DbContextOptions<VideoForensicsDbContext> options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite("Data Source=:memory:")
                .UseLoggerFactory(loggerFactory)
                .Options;

            // Act: the model is built on first access.
            using var db = new VideoForensicsDbContext(options);
            IEntityType? logsOnlyEntity = db.Model.FindEntityType(typeof(ProviderApiCallRecord));

            // Assert
            Assert.Null(logsOnlyEntity);
            Assert.Empty(warnings);
        }

        /// <summary>Collects the warning messages of one logger category.</summary>
        private sealed class WarningCollector : ILoggerProvider
        {
            private readonly string _category;
            private readonly ConcurrentQueue<string> _warnings;

            public WarningCollector(string category, ConcurrentQueue<string> warnings)
            {
                _category = category;
                _warnings = warnings;
            }

            public ILogger CreateLogger(string categoryName) => new Collector(categoryName == _category, _warnings);

            public void Dispose()
            {
            }

            private sealed class Collector : ILogger
            {
                private readonly bool _watch;
                private readonly ConcurrentQueue<string> _warnings;

                public Collector(bool watch, ConcurrentQueue<string> warnings)
                {
                    _watch = watch;
                    _warnings = warnings;
                }

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => _watch;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    if (_watch && logLevel == LogLevel.Warning)
                    {
                        _warnings.Enqueue(formatter(state, exception));
                    }
                }
            }
        }
    }
}
