using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Generic notes endpoints for jamming incidents and media flags. Case notes keep their own routes in
    /// <see cref="CaseEndpoints"/>; this group covers the other <see cref="NoteTargetKind"/> values.
    ///
    /// All endpoints require paired-device authentication. Writing a note requires Review or higher.
    /// The note author is always the OperatorId claim (401 if missing).
    /// </summary>
    public static class NoteEndpoints
    {
        /// <summary>Maps the /api/v1/notes/{kind}/{targetId} routes.</summary>
        /// <param name="app">The web application to add the routes to.</param>
        public static void MapNoteEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/notes").RequireAuthorization();

            _ = group.MapGet("/{kind}/{targetId:guid}", ListNotesAsync)
                .RequireRateLimiting("default")
                .WithSummary("List notes")
                .WithDescription("Retrieves all notes on a jamming incident or media flag, oldest first.");

            _ = group.MapPost("/{kind}/{targetId:guid}", AddNoteAsync)
                .RequireAuthorization(VideoForensicsPolicies.Review)
                .RequireRateLimiting("default")
                .WithSummary("Add note")
                .WithDescription("Appends a note to a jamming incident or media flag. Notes cannot be edited or deleted.");
        }

        /// <summary>Handler for GET /{kind}/{targetId:guid}</summary>
        public static async Task<IResult> ListNotesAsync(
            string kind,
            Guid targetId,
            ICaseRepository cases,
            IJammingRepository jamming,
            IMediaFlagWriteRepository flags,
            INoteRepository notes,
            CancellationToken ct)
        {
            if (!TryParseKind(kind, out NoteTargetKind parsedKind))
            {
                return Results.BadRequest("Unknown note target kind.");
            }

            if (!await TargetExistsAsync(parsedKind, targetId, cases, jamming, flags, ct))
            {
                return Results.NotFound();
            }

            IReadOnlyList<Note> result = await notes.ListAsync(parsedKind, targetId, ct);
            return Results.Ok(result.Select(n => n.ToNoteDto()).ToList());
        }

        /// <summary>Handler for POST /{kind}/{targetId:guid}</summary>
        public static async Task<IResult> AddNoteAsync(
            string kind,
            Guid targetId,
            AddNoteRequest request,
            ICaseRepository cases,
            IJammingRepository jamming,
            IMediaFlagWriteRepository flags,
            INoteRepository notes,
            HttpContext context,
            CancellationToken ct)
        {
            if (!TryParseKind(kind, out NoteTargetKind parsedKind))
            {
                return Results.BadRequest("Unknown note target kind.");
            }

            string? addedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(addedBy))
            {
                return Results.Unauthorized();
            }

            // The store does not check that the target exists (for Case it checks only in AddAsync), so the endpoint checks first.
            if (!await TargetExistsAsync(parsedKind, targetId, cases, jamming, flags, ct))
            {
                return Results.NotFound();
            }

            try
            {
                // The repository enforces the body rules (non-blank, at most 4000 characters after trimming).
                Note note = await notes.AddAsync(parsedKind, targetId, request.Body, addedBy, ct);
                return Results.Created($"/api/v1/notes/{parsedKind}/{targetId}/{note.Id}", note.ToNoteDto());
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }

        /// <summary>
        /// Parses a route kind by name, ignoring case. Numeric values are rejected because the enum's
        /// underlying values are not part of the API.
        /// </summary>
        private static bool TryParseKind(string kind, out NoteTargetKind parsed)
        {
            return Enum.TryParse<NoteTargetKind>(kind, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
        }

        /// <summary>Checks that the target record exists for the given kind.</summary>
        private static async Task<bool> TargetExistsAsync(
            NoteTargetKind kind,
            Guid targetId,
            ICaseRepository cases,
            IJammingRepository jamming,
            IMediaFlagWriteRepository flags,
            CancellationToken ct)
        {
            return kind switch
            {
                NoteTargetKind.Case => await cases.GetAsync(targetId, ct) is not null,
                NoteTargetKind.JammingIncident => await jamming.GetIncidentAsync(targetId, ct) is not null,
                NoteTargetKind.MediaFlag => await flags.GetAsync(targetId, ct) is not null,
                _ => false,
            };
        }
    }
}
