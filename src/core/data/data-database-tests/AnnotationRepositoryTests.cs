using Microsoft.Extensions.Logging;
using Xunit;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Tests
{
    public class AnnotationRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private AnnotationRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new AnnotationRepository(_fixture.Factory, loggerFactory.CreateLogger<AnnotationRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task AnnotationRepository_AddAsync_CreatesAnnotation()
        {
            var entityId = Guid.NewGuid();

            var annotation = await _repository.AddAsync("MediaItem", entityId, "mcp:face", "person", "John Doe", CancellationToken.None);

            Assert.NotNull(annotation);
            Assert.Equal("MediaItem", annotation.EntityType);
            Assert.Equal(entityId, annotation.EntityId);
            Assert.Equal("mcp:face", annotation.Source);
            Assert.Equal("person", annotation.Key);
            Assert.Equal("John Doe", annotation.Value);
        }

        [Fact]
        public async Task AnnotationRepository_GetAsync_FindsAnnotation()
        {
            var entityId = Guid.NewGuid();
            var annotation = await _repository.AddAsync("Event", entityId, "source1", "key1", "value1", CancellationToken.None);

            var retrieved = await _repository.GetAsync(annotation.Id, CancellationToken.None);

            Assert.NotNull(retrieved);
            Assert.Equal(annotation.Id, retrieved.Id);
            Assert.Equal("Event", retrieved.EntityType);
        }

        [Fact]
        public async Task AnnotationRepository_GetForEntityAsync_ReturnsAllForEntity()
        {
            var entityId = Guid.NewGuid();
            var otherEntityId = Guid.NewGuid();

            await _repository.AddAsync("MediaItem", entityId, "src1", "key1", "val1", CancellationToken.None);
            await _repository.AddAsync("MediaItem", entityId, "src2", "key2", "val2", CancellationToken.None);
            await _repository.AddAsync("MediaItem", otherEntityId, "src3", "key3", "val3", CancellationToken.None);

            var annotations = await _repository.GetForEntityAsync("MediaItem", entityId, CancellationToken.None);

            Assert.Equal(2, annotations.Count);
        }

        [Fact]
        public async Task AnnotationRepository_SearchAsync_ByKeyOnly()
        {
            await _repository.AddAsync("MediaItem", Guid.NewGuid(), "src1", "detected_person", "Alice", CancellationToken.None);
            await _repository.AddAsync("MediaItem", Guid.NewGuid(), "src2", "detected_person", "Bob", CancellationToken.None);
            await _repository.AddAsync("MediaItem", Guid.NewGuid(), "src3", "detected_object", "Car", CancellationToken.None);

            var results = await _repository.SearchAsync("detected_person", null, CancellationToken.None);

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task AnnotationRepository_SearchAsync_ByKeyAndValue()
        {
            await _repository.AddAsync("MediaItem", Guid.NewGuid(), "face_recognition", "person_name", "Alice", CancellationToken.None);
            await _repository.AddAsync("MediaItem", Guid.NewGuid(), "face_recognition", "person_name", "Alice", CancellationToken.None);
            await _repository.AddAsync("MediaItem", Guid.NewGuid(), "face_recognition", "person_name", "Bob", CancellationToken.None);

            var results = await _repository.SearchAsync("person_name", "Alice", CancellationToken.None);

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task AnnotationRepository_DeleteAsync_RemovesAnnotation()
        {
            var annotation = await _repository.AddAsync("MediaItem", Guid.NewGuid(), "src", "key", "val", CancellationToken.None);

            await _repository.DeleteAsync(annotation.Id, CancellationToken.None);

            var retrieved = await _repository.GetAsync(annotation.Id, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task AnnotationRepository_DeleteForEntityAsync_RemovesAllForEntity()
        {
            var entityId = Guid.NewGuid();
            var otherEntityId = Guid.NewGuid();

            await _repository.AddAsync("MediaItem", entityId, "src1", "key1", "val1", CancellationToken.None);
            await _repository.AddAsync("MediaItem", entityId, "src2", "key2", "val2", CancellationToken.None);
            await _repository.AddAsync("MediaItem", otherEntityId, "src3", "key3", "val3", CancellationToken.None);

            await _repository.DeleteForEntityAsync("MediaItem", entityId, CancellationToken.None);

            var forEntity = await _repository.GetForEntityAsync("MediaItem", entityId, CancellationToken.None);
            var forOther = await _repository.GetForEntityAsync("MediaItem", otherEntityId, CancellationToken.None);

            Assert.Empty(forEntity);
            Assert.Single(forOther);
        }

    }
}
