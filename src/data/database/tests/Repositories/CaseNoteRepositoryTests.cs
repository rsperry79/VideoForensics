using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Case notes are append-only multi-line text on a case, each with its author and write time.</summary>
    public class CaseNoteRepository_Tests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ILoggerFactory _loggerFactory = null!;
        private CaseRepository _cases = null!;
        private CaseNoteRepository _notes = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b => { });
            var actionLog = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
            _cases = new CaseRepository(_fixture.Factory, actionLog, _loggerFactory.CreateLogger<CaseRepository>());
            _notes = new CaseNoteRepository(_fixture.Factory, _loggerFactory.CreateLogger<CaseNoteRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            _loggerFactory.Dispose();
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private Task<ForensicCase> NewCaseAsync(string number)
        {
            return _cases.CreateAsync(number, "Title", null, null, null, null, [], "user@example.com", CancellationToken.None);
        }

        [Fact]
        public async Task CaseNoteRepository_Add_KeepsMultiLineBody()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-1");

            CaseNote added = await _notes.AddAsync(forensicCase.Id, "line one\nline two", "user@example.com", CancellationToken.None);
            IReadOnlyList<CaseNote> listed = await _notes.ListForCaseAsync(forensicCase.Id, CancellationToken.None);

            CaseNote stored = Assert.Single(listed);
            Assert.Equal(added.Id, stored.Id);
            Assert.Equal("line one\nline two", stored.Body);
            Assert.Equal("user@example.com", stored.AddedBy);
        }

        [Fact]
        public async Task CaseNoteRepository_Add_RejectsEmptyBody()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-2");

            _ = await Assert.ThrowsAsync<ArgumentException>(
                () => _notes.AddAsync(forensicCase.Id, "   ", "user@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task CaseNoteRepository_Add_RejectsBodyOverLimit()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-3");

            _ = await Assert.ThrowsAsync<ArgumentException>(
                () => _notes.AddAsync(forensicCase.Id, new string('a', 4001), "user@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task CaseNoteRepository_List_IsOldestFirst()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-4");
            _ = await _notes.AddAsync(forensicCase.Id, "first", "user@example.com", CancellationToken.None);
            // Timestamps come from DateTime.UtcNow, which can tie on Windows; a short gap keeps the add order observable.
            await Task.Delay(20);
            _ = await _notes.AddAsync(forensicCase.Id, "second", "user@example.com", CancellationToken.None);

            IReadOnlyList<CaseNote> listed = await _notes.ListForCaseAsync(forensicCase.Id, CancellationToken.None);

            Assert.Equal(new[] { "first", "second" }, listed.Select(n => n.Body));
        }

        [Fact]
        public async Task CaseNoteRepository_Add_UnknownCase_Throws()
        {
            _ = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _notes.AddAsync(Guid.NewGuid(), "note", "user@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task CaseNoteRepository_Add_ClosedCase_Throws()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-6");
            await _cases.CloseAsync(forensicCase.Id, "user@example.com", CancellationToken.None);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notes.AddAsync(forensicCase.Id, "note", "user@example.com", CancellationToken.None));
        }
    }
}
