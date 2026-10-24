using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Utils.Testing.Tests
{
    /// <summary>The shared fixture opens a usable in-memory database with the schema already created.</summary>
    public class SqliteInMemoryFixtureTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task Factory_CreatesContextThatCanQueryTheSchema()
        {
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync(CancellationToken.None);

            Assert.Empty(db.Events.ToList());
        }

        [Fact]
        public void EncryptionProvider_IsAvailableAfterInitialize()
        {
            Assert.NotNull(_fixture.EncryptionProvider);
        }
    }
}
