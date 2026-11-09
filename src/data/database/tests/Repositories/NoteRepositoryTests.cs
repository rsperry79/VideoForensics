using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>Notes are append-only multi-line text on a target (case, jamming incident or media flag), each with its author and write time.</summary>
    public class NoteRepository_Tests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ILoggerFactory _loggerFactory = null!;
        private CaseRepository _cases = null!;
        private NoteRepository _notes = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = LoggerFactory.Create(b => { });
            var actionLog = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
            var unitOfWork = new UnitOfWork(_fixture.Factory, new TestServiceProvider(_fixture, _loggerFactory), _loggerFactory.CreateLogger<UnitOfWork>());
            _cases = new CaseRepository(unitOfWork, actionLog, _loggerFactory.CreateLogger<CaseRepository>());
            _notes = new NoteRepository(unitOfWork, _loggerFactory.CreateLogger<NoteRepository>());
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
        public async Task NoteRepository_Add_KeepsMultiLineBody()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-1");

            Note added = await _notes.AddAsync(NoteTargetKind.Case, forensicCase.Id, "line one\nline two", "user@example.com", CancellationToken.None);
            IReadOnlyList<Note> listed = await _notes.ListAsync(NoteTargetKind.Case, forensicCase.Id, CancellationToken.None);

            Note stored = Assert.Single(listed);
            Assert.Equal(added.Id, stored.Id);
            Assert.Equal("line one\nline two", stored.Body);
            Assert.Equal("user@example.com", stored.AddedBy);
        }

        [Fact]
        public async Task NoteRepository_Add_RejectsEmptyBody()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-2");

            _ = await Assert.ThrowsAsync<ArgumentException>(
                () => _notes.AddAsync(NoteTargetKind.Case, forensicCase.Id, "   ", "user@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task NoteRepository_Add_RejectsBodyOverLimit()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-3");

            _ = await Assert.ThrowsAsync<ArgumentException>(
                () => _notes.AddAsync(NoteTargetKind.Case, forensicCase.Id, new string('a', 4001), "user@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task NoteRepository_List_IsOldestFirst()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-4");
            _ = await _notes.AddAsync(NoteTargetKind.Case, forensicCase.Id, "first", "user@example.com", CancellationToken.None);
            // Timestamps come from DateTime.UtcNow, which can tie on Windows; a short gap keeps the add order observable.
            await Task.Delay(20);
            _ = await _notes.AddAsync(NoteTargetKind.Case, forensicCase.Id, "second", "user@example.com", CancellationToken.None);

            IReadOnlyList<Note> listed = await _notes.ListAsync(NoteTargetKind.Case, forensicCase.Id, CancellationToken.None);

            Assert.Equal(new[] { "first", "second" }, listed.Select(n => n.Body));
        }

        [Fact]
        public async Task NoteRepository_Add_UnknownCase_Throws()
        {
            _ = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _notes.AddAsync(NoteTargetKind.Case, Guid.NewGuid(), "note", "user@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task NoteRepository_Add_ClosedCase_Throws()
        {
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-6");
            await _cases.CloseAsync(forensicCase.Id, "user@example.com", CancellationToken.None);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notes.AddAsync(NoteTargetKind.Case, forensicCase.Id, "note", "user@example.com", CancellationToken.None));
        }

        [Fact]
        public async Task NoteRepository_List_JammingIncidentNotesOnlyUnderOwnTarget()
        {
            var incidentId = Guid.NewGuid();
            var otherIncidentId = Guid.NewGuid();

            _ = await _notes.AddAsync(NoteTargetKind.JammingIncident, incidentId, "incident note", "user@example.com", CancellationToken.None);

            IReadOnlyList<Note> own = await _notes.ListAsync(NoteTargetKind.JammingIncident, incidentId, CancellationToken.None);
            IReadOnlyList<Note> other = await _notes.ListAsync(NoteTargetKind.JammingIncident, otherIncidentId, CancellationToken.None);

            Note stored = Assert.Single(own);
            Assert.Equal("incident note", stored.Body);
            Assert.Equal(NoteTargetKind.JammingIncident, stored.TargetKind);
            Assert.Empty(other);
        }

        [Fact]
        public async Task NoteRepository_List_KindsDoNotMix()
        {
            // The shared ID is a real case, so the case note passes the existence check.
            ForensicCase forensicCase = await NewCaseAsync("CASE-NOTE-5");
            Guid sharedId = forensicCase.Id;

            _ = await _notes.AddAsync(NoteTargetKind.Case, sharedId, "case note", "user@example.com", CancellationToken.None);
            _ = await _notes.AddAsync(NoteTargetKind.MediaFlag, sharedId, "flag note", "user@example.com", CancellationToken.None);

            IReadOnlyList<Note> cases = await _notes.ListAsync(NoteTargetKind.Case, sharedId, CancellationToken.None);
            IReadOnlyList<Note> flags = await _notes.ListAsync(NoteTargetKind.MediaFlag, sharedId, CancellationToken.None);

            Assert.Equal(new[] { "case note" }, cases.Select(n => n.Body));
            Assert.Equal(new[] { "flag note" }, flags.Select(n => n.Body));
        }
    }
}
