using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Forensic case management endpoints: creating cases, managing scope and device sets,
    /// pinning/unpinning evidence items, and closing/reopening cases with chain-of-custody
    /// tracking via ActionLog.
    ///
    /// All endpoints require paired-device authentication (RequireAuthorization()).
    /// Write operations require Review or higher role; close/reopen require Admin.
    /// Actor is always the OperatorId claim (401 if missing).
    /// </summary>
    public static class CaseEndpoints
    {
        public static void MapCaseEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/v1/cases").RequireAuthorization();

            // Case read operations
            _ = group.MapGet("/", GetCasesAsync)
                .RequireRateLimiting("default")
                .WithSummary("List cases")
                .WithDescription("Retrieves all cases, optionally filtered by status.");

            _ = group.MapGet("/{id:guid}", GetCaseAsync)
                .RequireRateLimiting("default")
                .WithSummary("Get case by ID")
                .WithDescription("Retrieves a single case by its unique identifier.");

            _ = group.MapGet("/by-number/{caseNumber}", GetCaseByNumberAsync)
                .RequireRateLimiting("default")
                .WithSummary("Get case by number")
                .WithDescription("Retrieves a single case by its case number.");

            _ = group.MapGet("/{id:guid}/devices", GetCaseDevicesAsync)
                .RequireRateLimiting("default")
                .WithSummary("Get case devices")
                .WithDescription("Retrieves the IDs of all devices in a case's scope.");

            _ = group.MapGet("/{id:guid}/items", GetCaseItemsAsync)
                .RequireRateLimiting("default")
                .WithSummary("Get case items")
                .WithDescription("Retrieves evidence items pinned to a case.");

            _ = group.MapGet("/containing", ListCasesContainingAsync)
                .RequireRateLimiting("default")
                .WithSummary("Find cases containing item")
                .WithDescription("Retrieves all cases that have an active pin of the specified kind and target ID.");

            // Case write operations
            _ = group.MapPost("/", CreateCaseAsync)
                .RequireAuthorization(VideoForensicsPolicies.Review)
                .RequireRateLimiting("default")
                .WithSummary("Create case")
                .WithDescription("Creates a new forensic case with optional scope and device set.");

            _ = group.MapPut("/{id:guid}", UpdateCaseDetailsAsync)
                .RequireAuthorization(VideoForensicsPolicies.Review)
                .RequireRateLimiting("default")
                .WithSummary("Update case details")
                .WithDescription("Updates a case's title, description, and lead operator.");

            _ = group.MapPut("/{id:guid}/scope", SetCaseScopeAsync)
                .RequireAuthorization(VideoForensicsPolicies.Review)
                .RequireRateLimiting("default")
                .WithSummary("Set case scope")
                .WithDescription("Updates a case's scope (time window and device set).");

            _ = group.MapPost("/{id:guid}/items", AddCaseItemAsync)
                .RequireAuthorization(VideoForensicsPolicies.Review)
                .RequireRateLimiting("default")
                .WithSummary("Add case item")
                .WithDescription("Pins an event or media item to a case.");

            _ = group.MapPost("/items/{itemId:guid}/remove", RemoveCaseItemAsync)
                .RequireAuthorization(VideoForensicsPolicies.Review)
                .RequireRateLimiting("default")
                .WithSummary("Remove case item")
                .WithDescription("Soft-removes an item from a case.");

            // Case lifecycle operations
            _ = group.MapPost("/{id:guid}/close", CloseCaseAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("default")
                .WithSummary("Close case")
                .WithDescription("Closes a case.");

            _ = group.MapPost("/{id:guid}/reopen", ReopenCaseAsync)
                .RequireAuthorization(VideoForensicsPolicies.Admin)
                .RequireRateLimiting("default")
                .WithSummary("Reopen case")
                .WithDescription("Reopens a closed case.");
        }

        /// <summary>Handler for GET /</summary>
        public static async Task<IResult> GetCasesAsync(
            string? status,
            ICaseRepository cases,
            CancellationToken ct)
        {
            CaseStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CaseStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                statusFilter = parsedStatus;
            }

            IReadOnlyList<ForensicCase> result = await cases.ListAsync(statusFilter, ct);
            return Results.Ok(result.Select(c => c.ToDto([])));
        }

        /// <summary>Handler for GET /{id:guid}</summary>
        public static async Task<IResult> GetCaseAsync(
            Guid id,
            ICaseRepository cases,
            CancellationToken ct)
        {
            ForensicCase? @case = await cases.GetAsync(id, ct);
            if (@case == null)
                return Results.NotFound();

            IReadOnlyList<Guid> deviceIds = await cases.GetDeviceIdsAsync(id, ct);
            return Results.Ok(@case.ToDto(deviceIds));
        }

        /// <summary>Handler for GET /by-number/{caseNumber}</summary>
        public static async Task<IResult> GetCaseByNumberAsync(
            string caseNumber,
            ICaseRepository cases,
            CancellationToken ct)
        {
            ForensicCase? @case = await cases.GetByNumberAsync(caseNumber, ct);
            if (@case == null)
                return Results.NotFound();

            IReadOnlyList<Guid> deviceIds = await cases.GetDeviceIdsAsync(@case.Id, ct);
            return Results.Ok(@case.ToDto(deviceIds));
        }

        /// <summary>Handler for GET /{id:guid}/devices</summary>
        public static async Task<IResult> GetCaseDevicesAsync(
            Guid id,
            ICaseRepository cases,
            CancellationToken ct)
        {
            IReadOnlyList<Guid> deviceIds = await cases.GetDeviceIdsAsync(id, ct);
            return Results.Ok(deviceIds);
        }

        /// <summary>Handler for GET /{id:guid}/items</summary>
        public static async Task<IResult> GetCaseItemsAsync(
            Guid id,
            bool includeRemoved = false,
            ICaseRepository? cases = null,
            CancellationToken ct = default)
        {
            if (cases == null)
                return Results.BadRequest("ICaseRepository not provided");

            IReadOnlyList<CaseItem> items = await cases.ListItemsAsync(id, includeRemoved, ct);
            return Results.Ok(items.Select(i => i.ToDto()));
        }

        /// <summary>Handler for GET /containing</summary>
        public static async Task<IResult> ListCasesContainingAsync(
            string? kind,
            Guid? targetId,
            ICaseRepository cases,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(kind) || !targetId.HasValue)
                return Results.BadRequest("kind and targetId query parameters are required");

            if (!Enum.TryParse<CaseItemKind>(kind, ignoreCase: true, out var itemKind))
                return Results.BadRequest("Invalid kind value");

            IReadOnlyList<ForensicCase> result = await cases.ListCasesContainingAsync(itemKind, targetId.Value, ct);
            // For simplicity, return with empty device lists since this is a list operation
            return Results.Ok(result.Select(c => c.ToDto([])));
        }

        /// <summary>Handler for POST /</summary>
        public static async Task<IResult> CreateCaseAsync(
            CreateCaseRequestDto request,
            ICaseRepository cases,
            HttpContext context,
            CancellationToken ct)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.CaseNumber) || request.CaseNumber.Length > 64)
                return Results.BadRequest("CaseNumber is required and must not exceed 64 characters");

            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 256)
                return Results.BadRequest("Title is required and must not exceed 256 characters");

            if (request.Description != null && request.Description.Length > 4000)
                return Results.BadRequest("Description must not exceed 4000 characters");

            if (request.ScopeFromUtc.HasValue && request.ScopeToUtc.HasValue && request.ScopeFromUtc > request.ScopeToUtc)
                return Results.BadRequest("ScopeFromUtc must be <= ScopeToUtc");

            string? createdBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(createdBy))
                return Results.Unauthorized();

            try
            {
                ForensicCase @case = await cases.CreateAsync(
                    request.CaseNumber,
                    request.Title,
                    request.Description,
                    request.LeadOperatorId,
                    request.ScopeFromUtc,
                    request.ScopeToUtc,
                    request.DeviceIds,
                    createdBy,
                    ct);

                IReadOnlyList<Guid> deviceIds = await cases.GetDeviceIdsAsync(@case.Id, ct);
                return Results.Created($"/api/v1/cases/{@case.Id}", @case.ToDto(deviceIds));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }

        /// <summary>Handler for PUT /{id:guid}</summary>
        public static async Task<IResult> UpdateCaseDetailsAsync(
            Guid id,
            UpdateCaseDetailsRequestDto request,
            ICaseRepository cases,
            HttpContext context,
            CancellationToken ct)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 256)
                return Results.BadRequest("Title is required and must not exceed 256 characters");

            if (request.Description != null && request.Description.Length > 4000)
                return Results.BadRequest("Description must not exceed 4000 characters");

            string? updatedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(updatedBy))
                return Results.Unauthorized();

            try
            {
                await cases.UpdateDetailsAsync(id, request.Title, request.Description, request.LeadOperatorId, updatedBy, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }

        /// <summary>Handler for PUT /{id:guid}/scope</summary>
        public static async Task<IResult> SetCaseScopeAsync(
            Guid id,
            SetCaseScopeRequestDto request,
            ICaseRepository cases,
            HttpContext context,
            CancellationToken ct)
        {
            // Validate input
            if (request.ScopeFromUtc.HasValue && request.ScopeToUtc.HasValue && request.ScopeFromUtc > request.ScopeToUtc)
                return Results.BadRequest("ScopeFromUtc must be <= ScopeToUtc");

            string? updatedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(updatedBy))
                return Results.Unauthorized();

            try
            {
                await cases.SetScopeAsync(id, request.ScopeFromUtc, request.ScopeToUtc, request.DeviceIds, updatedBy, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }

        /// <summary>Handler for POST /{id:guid}/items</summary>
        public static async Task<IResult> AddCaseItemAsync(
            Guid id,
            AddCaseItemRequestDto request,
            ICaseRepository cases,
            HttpContext context,
            CancellationToken ct)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Kind))
                return Results.BadRequest("Kind is required");

            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1024)
                return Results.BadRequest("Reason is required and must not exceed 1024 characters");

            if (!Enum.TryParse<CaseItemKind>(request.Kind, ignoreCase: true, out var itemKind))
                return Results.BadRequest("Invalid Kind value");

            string? addedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(addedBy))
                return Results.Unauthorized();

            try
            {
                CaseItem item = await cases.AddItemAsync(id, itemKind, request.TargetId, request.Reason, addedBy, ct);
                return Results.Created($"/api/v1/cases/{id}/items/{item.Id}", item.ToDto());
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }

        /// <summary>Handler for POST /items/{itemId:guid}/remove</summary>
        public static async Task<IResult> RemoveCaseItemAsync(
            Guid itemId,
            RemoveCaseItemRequestDto request,
            ICaseRepository cases,
            HttpContext context,
            CancellationToken ct)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1024)
                return Results.BadRequest("Reason is required and must not exceed 1024 characters");

            string? removedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(removedBy))
                return Results.Unauthorized();

            try
            {
                await cases.RemoveItemAsync(itemId, removedBy, request.Reason, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }

        /// <summary>Handler for POST /{id:guid}/close</summary>
        public static async Task<IResult> CloseCaseAsync(
            Guid id,
            ICaseRepository cases,
            HttpContext context,
            CancellationToken ct)
        {
            string? closedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(closedBy))
                return Results.Unauthorized();

            try
            {
                await cases.CloseAsync(id, closedBy, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }

        /// <summary>Handler for POST /{id:guid}/reopen</summary>
        public static async Task<IResult> ReopenCaseAsync(
            Guid id,
            ICaseRepository cases,
            HttpContext context,
            CancellationToken ct)
        {
            string? reopenedBy = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            if (string.IsNullOrWhiteSpace(reopenedBy))
                return Results.Unauthorized();

            try
            {
                await cases.ReopenAsync(id, reopenedBy, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        }
    }
}
