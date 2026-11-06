using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Append-only repository for notes on a case, jamming incident or media flag. There are deliberately no edit or delete operations.</summary>
    public class NoteRepository : INoteRepository
    {
        /// <summary>Maximum note length in characters, matching the database column limit.</summary>
        public const int MaxBodyLength = 4000;

        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<NoteRepository> _logger;

        /// <summary>Initializes a new instance of the NoteRepository.</summary>
        public NoteRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<NoteRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Appends a note to a target. Only case targets are checked for existence and state.</summary>
        public async Task<Note> AddAsync(NoteTargetKind kind, Guid targetId, string body, string addedBy, CancellationToken ct)
        {
            // Validate before touching the database so bad input never costs a round trip.
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("Note body is required.", nameof(body));
            }

            string trimmedBody = body.Trim();
            if (trimmedBody.Length > MaxBodyLength)
            {
                throw new ArgumentException($"Note body cannot exceed {MaxBodyLength} characters.", nameof(body));
            }

            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                string? caseNumber = null;
                if (kind == NoteTargetKind.Case)
                {
                    ForensicCase? forensicCase = await db.Cases
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == targetId, ct);
                    if (forensicCase is null)
                    {
                        throw new KeyNotFoundException($"Case {targetId} not found.");
                    }

                    if (forensicCase.Status == CaseStatus.Closed)
                    {
                        throw new InvalidOperationException($"Cannot add notes to a closed case {targetId}.");
                    }

                    caseNumber = forensicCase.CaseNumber;
                }

                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    TargetKind = kind,
                    TargetId = targetId,
                    Body = trimmedBody,
                    AddedBy = addedBy,
                    AddedAtUtc = DateTime.UtcNow,
                };

                _ = db.Notes.Add(note);
                _ = await db.SaveChangesAsync(ct);

                if (caseNumber is not null)
                {
                    _logger.LogInformation("Note added to case {CaseNumber} by {AddedBy}", caseNumber, addedBy);
                }
                else
                {
                    _logger.LogInformation("Note added to {TargetKind} by {AddedBy}", kind, addedBy);
                }

                return note;
            }
            catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
            {
                // Log the type and message only: no stack traces, no GUIDs.
                _logger.LogError("Error adding note: {ExceptionType}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
        }

        /// <summary>Lists all notes for a target of the given kind, oldest first.</summary>
        public async Task<IReadOnlyList<Note>> ListAsync(NoteTargetKind kind, Guid targetId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Notes
                .AsNoTracking()
                .Where(n => n.TargetKind == kind && n.TargetId == targetId)
                .OrderBy(n => n.AddedAtUtc)
                .ToListAsync(ct);
        }
    }
}
