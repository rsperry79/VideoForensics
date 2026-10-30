using Microsoft.AspNetCore.Routing;

using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Database administration for the System &gt; Database page. Super Admin on the local tier only, like Storage and Logs.
    /// Compact, repair, purge and chain repair change the database, so each runs as a maintenance run: new work is held back
    /// while it runs. Purge and chain repair also need a step-up. Stats and validate only read.
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

            _ = group.MapPost("/compact", (MaintenanceCoordinator coordinator, CancellationToken ct) =>
                    RunMaintenanceAsync(coordinator, "Compacting the database", async (sp, token) =>
                    {
                        await sp.GetRequiredService<IDatabaseAdministrationService>().CompactAsync(token);
                        return Results.NoContent();
                    }, ct))
                .WithName("CompactDatabase")
                .WithSummary("Rebuild the database file to reclaim free pages");

            _ = group.MapPost("/repair", (MaintenanceCoordinator coordinator, CancellationToken ct) =>
                    RunMaintenanceAsync(coordinator, "Checking and rebuilding the database", async (sp, token) =>
                        Results.Ok(new RepairVerdictDto(await sp.GetRequiredService<IDatabaseAdministrationService>().RepairAsync(token))), ct))
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

            _ = group.MapPost("/purge-missing", (PurgeMissingRequest request, MaintenanceCoordinator coordinator, CancellationToken ct) =>
                    {
                        if (request.MediaItemIds.Count == 0 || request.MediaItemIds.Count > MaxPurgeBatch)
                        {
                            return Task.FromResult(Results.ValidationProblem(new Dictionary<string, string[]>
                            {
                                [nameof(request.MediaItemIds)] = [$"Send between 1 and {MaxPurgeBatch} ids."],
                            }));
                        }

                        return RunMaintenanceAsync(coordinator, "Purging missing media records", async (sp, token) =>
                            Results.Ok(new PurgeMissingResultDto(await sp.GetRequiredService<IDatabaseAdministrationService>().PurgeMissingMediaAsync(request.MediaItemIds, token))), ct);
                    })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithName("PurgeMissingMedia")
                .WithSummary("Purge media rows whose files are missing. Legal holds are skipped.");

            _ = group.MapPost("/repair-chain", (MaintenanceCoordinator coordinator, CancellationToken ct) =>
                    RunMaintenanceAsync(coordinator, "Repairing the action log chain", async (sp, token) =>
                    {
                        ChainRepairResult result = await sp.GetRequiredService<IDatabaseAdministrationService>().RepairChainAsync(token);
                        return Results.Ok(new ChainRepairResultDto(
                            result.BridgesAdded,
                            result.Unrepairable is null ? null : ToDto(result.Unrepairable)));
                    }, ct))
                .AddEndpointFilter<StepUpEndpointFilter>()
                .WithName("RepairActionLogChain")
                .WithSummary("Bridge action log gaps with logged ChainRepaired entries. Altered entries are never bridged.");
        }

        // Turns an overlapping run into 409 Conflict. Other failures are not caught here, so they surface as server errors.
        private static async Task<IResult> RunMaintenanceAsync(
            MaintenanceCoordinator coordinator,
            string description,
            Func<IServiceProvider, CancellationToken, Task<IResult>> operation,
            CancellationToken ct)
        {
            try
            {
                return await coordinator.RunAsync(description, operation, ct);
            }
            catch (MaintenanceInProgressException ex)
            {
                return Results.Conflict(ex.Message);
            }
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
