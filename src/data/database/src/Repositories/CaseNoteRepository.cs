using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Append-only repository for case notes. There are deliberately no edit or delete operations.</summary>
    public class CaseNoteRepository : ICaseNoteRepository
    {
        /// <summary>Maximum note length in characters, matching the database column limit.</summary>
        public const int MaxBodyLength = 4000;

        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<CaseNoteRepository> _logger;

        /// <summary>Initializes a new instance of the CaseNoteRepository.</summary>
        public CaseNoteRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<CaseNoteRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Appends a note to an open case.</summary>
        public async Task<CaseNote> AddAsync(Guid caseId, string body, string addedBy, CancellationToken ct)
        {
            // Validate before touching the database so bad input never costs a round trip.
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("Case note body cannot be empty.", nameof(body));
            }

            string trimmedBody = body.Trim();
            if (trimmedBody.Length > MaxBodyLength)
            {
                throw new ArgumentException($"Case note body cannot exceed {MaxBodyLength} characters.", nameof(body));
            }

            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                ForensicCase? forensicCase = await db.Cases
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == caseId, ct);
                if (forensicCase is null)
                {
                    throw new KeyNotFoundException($"Case {caseId} not found.");
                }

                if (forensicCase.Status == CaseStatus.Closed)
                {
                    throw new InvalidOperationException($"Cannot add notes to a closed case {caseId}.");
                }

                var note = new CaseNote
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    Body = trimmedBody,
                    AddedBy = addedBy,
                    AddedAtUtc = DateTime.UtcNow,
                };

                _ = db.CaseNotes.Add(note);
                _ = await db.SaveChangesAsync(ct);

                _logger.LogInformation("Case note added to case {CaseNumber} by {AddedBy}", forensicCase.CaseNumber, addedBy);
                return note;
            }
            catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
            {
                // Log the type and message only: no stack traces, no GUIDs.
                _logger.LogError("Error adding case note: {ExceptionType}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
        }

        /// <summary>Lists all notes for a case, oldest first.</summary>
        public async Task<IReadOnlyList<CaseNote>> ListForCaseAsync(Guid caseId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.CaseNotes
                .AsNoTracking()
                .Where(n => n.CaseId == caseId)
                .OrderBy(n => n.AddedAtUtc)
                .ToListAsync(ct);
        }
    }
}
