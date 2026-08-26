using Microsoft.Extensions.Logging;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class ActionLogRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ActionLogRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new ActionLogRepository(_fixture.Factory, loggerFactory.CreateLogger<ActionLogRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task ActionLogRepository_AppendAsync_CreatesEntry()
        {
            var entry = await _repository.AppendAsync(
                "TestActor",
                ActorType.Human,
                "TestAction",
                "TestEntity",
                Guid.NewGuid(),
                null,
                CancellationToken.None);

            Assert.NotNull(entry);
            Assert.Equal("TestActor", entry.Actor);
            Assert.Equal("TestAction", entry.Action);
        }

        [Fact]
        public async Task ActionLogRepository_AppendAsync_FirstEntryHasNoPreviousHash()
        {
            var entry = await _repository.AppendAsync(
                "Actor1",
                ActorType.Human,
                "Action1",
                "Entity1",
                Guid.NewGuid(),
                null,
                CancellationToken.None);

            Assert.Null(entry.PreviousEntryHash);
            Assert.NotNull(entry.EntryHash);
        }

        [Fact]
        public async Task ActionLogRepository_AppendAsync_SecondEntryLinksToFirst()
        {
            var entry1 = await _repository.AppendAsync(
                "Actor1",
                ActorType.Human,
                "Action1",
                "Entity1",
                Guid.NewGuid(),
                null,
                CancellationToken.None);

            var entry2 = await _repository.AppendAsync(
                "Actor2",
                ActorType.Human,
                "Action2",
                "Entity2",
                Guid.NewGuid(),
                null,
                CancellationToken.None);

            Assert.Equal(entry1.EntryHash, entry2.PreviousEntryHash);
        }

        [Fact]
        public async Task ActionLogRepository_VerifyChainIntegrity_EmptyDatabaseReturnsTrue()
        {
            var isValid = await _repository.VerifyChainIntegrityAsync(CancellationToken.None);

            Assert.True(isValid);
        }

        [Fact]
        public async Task ActionLogRepository_VerifyChainIntegrity_EmptyChainReturnsTrue()
        {
            var isValid = await _repository.VerifyChainIntegrityAsync(CancellationToken.None);

            Assert.True(isValid);
        }

        [Fact]
        public async Task ActionLogRepository_GetHistoryForEntity_ReturnsEntriesInReverseOrder()
        {
            var entityId = Guid.NewGuid();

            await _repository.AppendAsync("A1", ActorType.Human, "Act1", "Ent", entityId, null, CancellationToken.None);
            await _repository.AppendAsync("A2", ActorType.Human, "Act2", "Ent", entityId, null, CancellationToken.None);
            await _repository.AppendAsync("A3", ActorType.Human, "Act3", "Ent", entityId, null, CancellationToken.None);

            var history = await _repository.GetHistoryForEntityAsync("Ent", entityId, CancellationToken.None);

            Assert.Equal(3, history.Count);
            Assert.Equal("A3", history[0].Actor);
            Assert.Equal("A2", history[1].Actor);
            Assert.Equal("A1", history[2].Actor);
        }

        [Fact]
        public async Task ActionLogRepository_ListAsync_ReturnsAll()
        {
            var entity1 = Guid.NewGuid();
            var entity2 = Guid.NewGuid();

            await _repository.AppendAsync("A1", ActorType.Human, "Act1", "Ent1", entity1, null, CancellationToken.None);
            await _repository.AppendAsync("A2", ActorType.Human, "Act2", "Ent2", entity2, null, CancellationToken.None);

            var list = await _repository.ListAsync(CancellationToken.None);

            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ActionLogRepository_GetAsync_FindsEntry()
        {
            var entry = await _repository.AppendAsync(
                "TestActor",
                ActorType.System,
                "TestAction",
                "TestEntity",
                null,
                "{\"key\":\"value\"}",
                CancellationToken.None);

            var retrieved = await _repository.GetAsync(entry.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(entry.Id, retrieved.Id);
            Assert.Equal("TestActor", retrieved.Actor);
            Assert.Equal(ActorType.System, retrieved.ActorType);
        }

        [Fact]
        public async Task ActionLogRepository_AppendAsync_WithDetails_PreservesJson()
        {
            var details = "{\"action\":\"download\",\"itemCount\":5}";
            var entry = await _repository.AppendAsync(
                "Downloader",
                ActorType.Human,
                "MediaDownloaded",
                "MediaItem",
                Guid.NewGuid(),
                details,
                CancellationToken.None);

            var retrieved = await _repository.GetAsync(entry.Id, CancellationToken.None);
            Assert.Equal(details, retrieved.DetailsJson);
        }

        [Fact]
        public async Task ActionLogRepository_EntryHash_DifferentForDifferentContent()
        {
            var entry1 = await _repository.AppendAsync("Actor1", ActorType.Human, "Action1", "Entity1", null, null, CancellationToken.None);
            var entry2 = await _repository.AppendAsync("Actor2", ActorType.Human, "Action2", "Entity2", null, null, CancellationToken.None);

            Assert.NotEqual(entry1.EntryHash, entry2.EntryHash);
        }

        [Fact]
        public async Task ActionLogRepository_HashChain_WithMultipleActors()
        {
            var entry1 = await _repository.AppendAsync("Human1", ActorType.Human, "Act1", "Ent", Guid.NewGuid(), null, CancellationToken.None);
            await Task.Delay(10);
            var entry2 = await _repository.AppendAsync("System1", ActorType.System, "Act2", "Ent", Guid.NewGuid(), null, CancellationToken.None);
            await Task.Delay(10);
            var entry3 = await _repository.AppendAsync("McpTool1", ActorType.McpTool, "Act3", "Ent", Guid.NewGuid(), null, CancellationToken.None);

            Assert.Equal(entry1.EntryHash, entry2.PreviousEntryHash);
            Assert.Equal(entry2.EntryHash, entry3.PreviousEntryHash);
        }
    }
}
