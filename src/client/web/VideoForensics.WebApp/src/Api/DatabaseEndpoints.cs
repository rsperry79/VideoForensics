using Microsoft.AspNetCore.Routing;

using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Database administration for the System &gt; Database page. Super Admin on the local tier only, like Storage and Logs.
    /// Purge and chain repair are destructive, so they also need a step-up. Validation changes nothing.
    /// </summary>
    public static class DatabaseEndpoints
    {
        // Bounds one purge request. A validation pass over a large archive should purge in batches.
        private const int MaxPurgeBatch = 5000;

        public static void MapDatabaseEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/database")
                .RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal)
                .WithTags("Database");

            _ = group.MapGet("/stats", async (IDatabaseAdministrationService admin, CancellationToken ct) =>
                {
                    DatabaseStatsResult stats = await admin.GetStatsAsync(ct);
                    return Results.Ok(new DatabaseStatsDto(
                        stats.File.Engine,
                        stats.File.Location,
                        stats.File.FileBytes,
                        stats.File.PageCount,
                        stats.File.FreePageCount,
                        stats.File.PageSize,
                        stats.File.JournalMode,
                        stats.MediaItemCount,
                        stats.PurgedMediaItemCount,
                        stats.ActionLogEntryCount));
                })
                .WithName("GetDatabaseStats")
                .WithSummary("Physical database stats and record counts");

            _ = group.MapPost("/compact", async (IDatabaseAdministrationService admin, CancellationToken ct) =>
                {
                    await admin.CompactAsync(ct);
                    return Results.NoContent();
                })
                .WithName("CompactDatabase")
                .WithSummary("Rebuild the database file to reclaim free pages");

            _ = group.MapPost("/repair", async (IDatabaseAdministrationService admin, CancellationToken ct) =>
                    Results.Ok(new RepairVerdictDto(await admin.RepairAsync(ct))))
                .WithName("RepairDatabase")
                .WithSummary("Integrity check and index rebuild");

            _ = group.MapGet("/validate", async (IDatabaseAdministrationService admin, CancellationToken ct) =>
                {
                    DatabaseValidationResult result = await admin.ValidateAsync(ct);
                    return Results.Ok(new DatabaseValidationDto(
                        result.MissingMedia.Select(m => new MissingMediaDto(m.MediaItemId, m.FileName, m.ResolvedPath)).ToList(),
                        ToDto(result.Chain),
                        result.IsClean));
                })
                .WithName("ValidateDatabase")
                .WithSummary("List media rows without files and check the action log chain. Changes nothing.");

            _ = group.MapPost("/purge-missing", async (PurgeMissingRequest request, IDatabaseAdministrationService admin, CancellationToken ct) =>
                {
                    if (request.MediaItemIds.Count == 0 || request.MediaItemIds.Count > MaxPurgeBatch)
                    {
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            [nameof(request.MediaItemIds)] = [$"Send between 1 and {MaxPurgeBatch} ids."],
                        });
                    }

                    int purged = await admin.PurgeMissingMediaAsync(request.MediaItemIds, ct);
                    return Results.Ok(new PurgeMissingResultDto(purged));
                })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithName("PurgeMissingMedia")
                .WithSummary("Purge media rows whose files are missing. Legal holds are skipped.");

            _ = group.MapPost("/repair-chain", async (IDatabaseAdministrationService admin, CancellationToken ct) =>
                {
                    ChainRepairResult result = await admin.RepairChainAsync(ct);
                    return Results.Ok(new ChainRepairResultDto(
                        result.BridgesAdded,
                        result.Unrepairable is null ? null : ToDto(result.Unrepairable)));
                })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithName("RepairActionLogChain")
                .WithSummary("Bridge action log gaps with logged ChainRepaired entries. Altered entries are never bridged.");
        }

        private static ChainCheckDto ToDto(ChainIntegrityResult chain)
        {
            return new ChainCheckDto(
                chain.IsValid,
                chain.EntriesChecked,
                chain.Break.ToString(),
                chain.FailedEntryId,
                chain.FailedEntryTimestampUtc);
        }
    }
}
