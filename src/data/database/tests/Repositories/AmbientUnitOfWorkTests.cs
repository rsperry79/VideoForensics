using Microsoft.Extensions.Logging;

using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// A read started inside a write inherits the write's ambient context through the async flow. If that read runs after the
    /// write has ended, the context is disposed, so the read must open its own context instead.
    /// </summary>
    public class AmbientUnitOfWorkTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b => _ = b.SetMinimumLevel(LogLevel.Warning));
        }

        public async ValueTask DisposeAsync()
        {
            _loggerFactory.Dispose();
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task ReadAsync_BackgroundReadStartedInsideWrite_AfterWriteEnds_OpensItsOwnContext()
        {
            var serviceProvider = new TestServiceProvider(_fixture, _loggerFactory);
            var unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, _loggerFactory.CreateLogger<UnitOfWork>(), null, null);
            var lateRead = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            _ = await unitOfWork.ExecuteAsync<object?>(context =>
            {
                // The background read captures this write's ambient context, then runs after the write has finished.
                _ = Task.Run(async () =>
                {
                    await Task.Delay(200);
                    try
                    {
                        IReadOnlyList<VideoForensics.Data.Common.Entities.MediaItem> items = await unitOfWork.ReadAsync(c => c.MediaItems.ListAsync(CancellationToken.None), CancellationToken.None);
                        lateRead.SetResult(items.Count);
                    }
                    catch (Exception ex)
                    {
                        lateRead.SetException(ex);
                    }
                });
                return Task.FromResult<object?>(null);
            }, CancellationToken.None);

            _ = await lateRead.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
