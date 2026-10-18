using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Services;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// User and provider account management (plan §5): listing users and provider accounts,
    /// creating new accounts, and managing account lifecycle. All endpoints require SuperAdmin+Local
    /// authorization. Create and delete operations require step-up authentication for security.
    /// </summary>
    public static class AccountEndpoints
    {
        public static void MapAccountEndpoints(this WebApplication app)
        {
            // IEventPullService is scoped, so the background pulls below open their own scope per run.
            // Resolving it here from the root provider would throw under scope validation.
            IServiceScopeFactory scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

            RouteGroupBuilder group = app.MapGroup("/api/v1/accounts").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocal);

            // User endpoints
            _ = group.MapGet("/users", async (IUserRepository users, CancellationToken ct) =>
                Results.Ok((await users.ListAsync(ct)).Select(x => x.ToDto())))
                .RequireRateLimiting("media")
                .WithSummary("List all users")
                .WithDescription("Retrieves all users in the system.");

            _ = group.MapGet("/users/{id:guid}", async (Guid id, IUserRepository users, CancellationToken ct) =>
            {
                User? user = await users.GetAsync(id, ct);
                return user == null ? Results.NotFound() : Results.Ok(user.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Get user by ID")
                .WithDescription("Retrieves a single user by its unique identifier.");

            _ = group.MapGet("/users/by-provider-key/{providerUserKey}", async (string providerUserKey, IUserRepository users, CancellationToken ct) =>
            {
                User? user = await users.GetByProviderKeyAsync(providerUserKey, ct);
                return user == null ? Results.NotFound() : Results.Ok(user.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Get user by provider key")
                .WithDescription("Retrieves a user by its provider-specific user key.");

            _ = group.MapPost("/users", async (
                CreateUserRequest request,
                IUserRepository users,
                CancellationToken ct) =>
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    ProviderUserKey = request.ProviderUserKey,
                    DisplayName = request.DisplayName,
                    Email = request.Email,
                    CreatedUtc = DateTime.UtcNow
                };
                await users.AddAsync(user, ct);
                return Results.Created($"/api/v1/accounts/users/{user.Id}", user.ToDto());
            })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media")
                .WithSummary("Create a new user")
                .WithDescription("Creates a new user account.");

            _ = group.MapPut("/users/{id:guid}", async (
                Guid id,
                UpdateUserRequest request,
                IUserRepository users,
                CancellationToken ct) =>
            {
                User? user = await users.GetAsync(id, ct);
                if (user == null)
                {
                    return Results.NotFound();
                }

                user.DisplayName = request.DisplayName;
                if (!string.IsNullOrEmpty(request.Email))
                {
                    user.Email = request.Email;
                }

                await users.UpdateAsync(user, ct);
                return Results.Ok(user.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Update a user")
                .WithDescription("Updates an existing user's information.");

            _ = group.MapDelete("/users/{id:guid}", async (Guid id, IUserRepository users, CancellationToken ct) =>
            {
                User? user = await users.GetAsync(id, ct);
                if (user == null)
                {
                    return Results.NotFound();
                }

                await users.DeleteAsync(id, ct);
                return Results.Ok();
            })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media")
                .WithSummary("Delete a user")
                .WithDescription("Deletes a user account.");

            // Provider account endpoints
            _ = group.MapGet("/provider-accounts", async (IProviderAccountRepository accounts, CancellationToken ct) =>
                Results.Ok((await accounts.ListAsync(ct)).Select(x => x.ToDto())))
                .RequireRateLimiting("media")
                .WithSummary("List all provider accounts")
                .WithDescription("Retrieves all provider accounts in the system.");

            _ = group.MapGet("/provider-accounts/active", async (IProviderAccountRepository accounts, CancellationToken ct) =>
                Results.Ok((await accounts.ListActiveAsync(ct)).Select(x => x.ToDto())))
                .RequireRateLimiting("media")
                .WithSummary("List active provider accounts")
                .WithDescription("Retrieves all active provider accounts.");

            // Declared before the "{id:guid}" routes; the guid constraint already keeps "removed" from colliding.
            _ = group.MapGet("/provider-accounts/removed", async (IProviderAccountRepository accounts, CancellationToken ct) =>
                Results.Ok((await accounts.ListRemovedAsync(ct)).Select(x => x.ToDto())))
                .RequireRateLimiting("media")
                .WithSummary("List removed provider accounts")
                .WithDescription("Retrieves provider accounts that were retired (soft-removed) and still retain their data.");

            _ = group.MapGet("/provider-accounts/{id:guid}/cases", async (Guid id, ICaseRepository cases, CancellationToken ct) =>
                Results.Ok((await cases.ListForProviderAccountAsync(id, ct)).Select(x => x.ToDto())))
                .RequireRateLimiting("media")
                .WithSummary("List cases attached to a provider account")
                .WithDescription("Retrieves the distinct cases with at least one scope device at a location the account can see, newest first.");

            _ = group.MapGet("/provider-accounts/{id:guid}/sync-runs", async (Guid id, IProviderSyncRunRepository syncRuns, CancellationToken ct) =>
                Results.Ok((await syncRuns.ListByProviderAccountIdAsync(id, ct)).Select(x => x.ToSummaryDto())))
                .RequireRateLimiting("media")
                .WithSummary("List sync runs for a provider account")
                .WithDescription("Retrieves every sync attempt for the account, newest first.");

            _ = group.MapGet("/provider-accounts/{id:guid}/sync-runs/{runId:guid}", async (Guid id, Guid runId, IProviderSyncRunRepository syncRuns, CancellationToken ct) =>
            {
                ProviderSyncRun? run = await syncRuns.GetAsync(runId, ct);

                // A run belonging to another account is reported as missing so runs cannot be read across accounts.
                return run == null || run.ProviderAccountId != id ? Results.NotFound() : Results.Ok(run.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Get a sync run for a provider account")
                .WithDescription("Retrieves a single sync attempt by ID. Returns 404 when the run does not belong to the account.");

            _ = group.MapGet("/provider-accounts/{id:guid}", async (Guid id, IProviderAccountRepository accounts, CancellationToken ct) =>
            {
                ProviderAccount? account = await accounts.GetAsync(id, ct);
                return account == null ? Results.NotFound() : Results.Ok(account.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Get provider account by ID")
                .WithDescription("Retrieves a provider account by its unique identifier.");

            _ = group.MapGet("/provider-accounts/by-user/{userId:guid}", async (Guid userId, IProviderAccountRepository accounts, CancellationToken ct) =>
                Results.Ok((await accounts.GetByUserIdAsync(userId, ct)).Select(x => x.ToDto())))
                .RequireRateLimiting("media")
                .WithSummary("Get provider accounts by user")
                .WithDescription("Retrieves all provider accounts linked to a specific user.");

            _ = group.MapGet("/provider-accounts/by-user-and-provider", async (Guid userId, string providerName, IProviderAccountRepository accounts, CancellationToken ct) =>
            {
                ProviderAccount? account = await accounts.GetByUserAndProviderAsync(userId, providerName, ct);
                return account == null ? Results.NotFound() : Results.Ok(account.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Get provider account by user and provider")
                .WithDescription("Retrieves a provider account by user ID and provider name.");

            _ = group.MapPost("/provider-accounts", async (
                CreateProviderAccountRequest request,
                IProviderAccountRepository accounts,
                CancellationToken ct) =>
            {
                var account = new ProviderAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    ProviderName = request.ProviderName,
                    LinkedUtc = DateTime.UtcNow,
                    LastSuccessfulAuthUtc = null,
                    IsActive = true,
                    LastDownloadTimeUtc = null
                };
                await accounts.AddAsync(account, ct);

                // Initial sync: restore the session, discover locations and devices, pull the full history.
                _ = Task.Run(() => RunInitialSyncAsync(scopeFactory, account.Id), CancellationToken.None);

                return Results.Created($"/api/v1/accounts/provider-accounts/{account.Id}", account.ToDto());
            })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media")
                .WithSummary("Create a new provider account link")
                .WithDescription("Creates a new link between a user and a provider account.");

            _ = group.MapPut("/provider-accounts/{id:guid}", async (
                Guid id,
                UpdateProviderAccountRequest request,
                IProviderAccountRepository accounts,
                CancellationToken ct) =>
            {
                ProviderAccount? account = await accounts.GetAsync(id, ct);
                if (account == null)
                {
                    return Results.NotFound();
                }

                if (request.LastSuccessfulAuthUtc.HasValue)
                {
                    account.LastSuccessfulAuthUtc = request.LastSuccessfulAuthUtc.Value;
                }

                if (request.LastDownloadTimeUtc.HasValue)
                {
                    account.LastDownloadTimeUtc = request.LastDownloadTimeUtc.Value;
                }

                account.IsActive = request.IsActive;

                await accounts.UpdateAsync(account, ct);
                return Results.Ok(account.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Update a provider account")
                .WithDescription("Updates an existing provider account's information.");

            _ = group.MapDelete("/provider-accounts/{id:guid}", async (Guid id, IProviderAccountRepository accounts, CancellationToken ct) =>
            {
                ProviderAccount? account = await accounts.GetAsync(id, ct);
                if (account == null)
                {
                    return Results.NotFound();
                }

                await accounts.DeleteAsync(id, ct);
                return Results.Ok();
            })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media")
                .WithSummary("Delete a provider account")
                .WithDescription("Deletes a provider account link.");

            _ = group.MapPost("/provider-accounts/{id:guid}/retire", async (Guid id, IProviderAccountRepository accounts, CancellationToken ct) =>
            {
                ProviderAccount? account = await accounts.GetAsync(id, ct);
                if (account == null)
                {
                    return Results.NotFound();
                }

                // Idempotent: an already-removed account is returned unchanged rather than re-stamped.
                if (account.RemovedUtc == null)
                {
                    await accounts.RetireAsync(id, DateTime.UtcNow, ct);
                    account = await accounts.GetAsync(id, ct) ?? account;
                }

                return Results.Ok(account.ToDto());
            })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media")
                .WithSummary("Retire a provider account")
                .WithDescription("Soft-removes the account: syncing stops but its data is kept. Idempotent.");

            _ = group.MapPost("/provider-accounts/{id:guid}/exports", async (
                Guid id,
                QueueAccountExportRequest request,
                IProviderAccountRepository accounts,
                IAccountExportService exports,
                CancellationToken ct) =>
            {
                ProviderAccount? account = await accounts.GetAsync(id, ct);
                if (account == null)
                {
                    return Results.NotFound();
                }

                // Retire first so the scheduler stops pulling for the account before the export runs.
                if (request.PurgeAfterExport && account.RemovedUtc == null)
                {
                    await accounts.RetireAsync(id, DateTime.UtcNow, ct);
                }

                Guid jobId = await exports.QueueExportAsync(id, request.PurgeAfterExport, ct);
                ExportJobStatus? status = await exports.GetStatusAsync(jobId, ct);
                ExportJobDto dto = status?.ToDto()
                    ?? new ExportJobDto(jobId, id, ExportJobState.Queued.ToString(), null, null, request.PurgeAfterExport, DateTime.UtcNow, null);
                return Results.Accepted($"/api/v1/accounts/exports/{jobId}", dto);
            })
                .AddEndpointFilter<StepUpEndpointFilter>()
                .RequireRateLimiting("media")
                .WithSummary("Export a provider account")
                .WithDescription("Queues a background export of the account's data, optionally purging the account afterwards.");

            _ = group.MapGet("/provider-accounts/{id:guid}/export-status", async (Guid id, IAccountExportService exports, CancellationToken ct) =>
            {
                ExportJobStatus? status = await exports.GetLatestStatusForAccountAsync(id, ct);
                return status == null ? Results.NotFound() : Results.Ok(status.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Get the latest export job for an account")
                .WithDescription("Returns the most recently queued export job for the account, or 404 when none is known (including after a server restart).");

            _ = group.MapGet("/exports/{jobId:guid}", async (Guid jobId, IAccountExportService exports, CancellationToken ct) =>
            {
                ExportJobStatus? status = await exports.GetStatusAsync(jobId, ct);
                return status == null ? Results.NotFound() : Results.Ok(status.ToDto());
            })
                .RequireRateLimiting("media")
                .WithSummary("Get account export job status")
                .WithDescription("Returns the status of a queued export job, or 404 when unknown (including after a server restart).");

            _ = group.MapPost("/provider-accounts/{id:guid}/initial-sync", async (
                Guid id,
                IProviderAccountRepository accounts,
                CancellationToken ct) =>
            {
                ProviderAccount? account = await accounts.GetAsync(id, ct);
                if (account == null)
                {
                    return Results.NotFound();
                }

                if (!account.IsActive)
                {
                    return Results.Conflict("Account is no longer active");
                }

                _ = Task.Run(() => RunInitialSyncAsync(scopeFactory, id), CancellationToken.None);
                return Results.Accepted();
            })
                .RequireRateLimiting("media")
                .WithSummary("Start the first full sync of a newly added account")
                .WithDescription("Restores the account's session, discovers its locations and devices, and pulls its full event history in the background.");

            _ = group.MapPost("/provider-accounts/{id:guid}/sync-now", async (
                Guid id,
                SyncNowRequest? body,
                IProviderAccountRepository accounts,
                CancellationToken ct) =>
            {
                // The body is optional: no body keeps the default incremental sync.
                AccountSyncRequest? request = null;
                if (body != null && !TryBuildSyncRequest(body, out request, out string? error))
                {
                    return Results.BadRequest(error);
                }

                ProviderAccount? account = await accounts.GetAsync(id, ct);
                if (account == null)
                {
                    return Results.NotFound();
                }

                if (!account.IsActive)
                {
                    return Results.Conflict("Account is no longer active");
                }

                // Queue background pull using fire-and-forget
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using IServiceScope scope = scopeFactory.CreateScope();

                        IAccountSyncRunner syncRunner = scope.ServiceProvider.GetRequiredService<IAccountSyncRunner>();
                        if (request == null)
                        {
                            await syncRunner.RunAsync(id, CancellationToken.None);
                        }
                        else
                        {
                            await syncRunner.RunAsync(id, request, CancellationToken.None);
                        }
                    }
                    catch
                    {
                        // Error captured in ProviderAccount.LastErrorMessage by EventPullService
                    }
                }, ct);

                return Results.Accepted(
                    null,
                    new SyncNowResponseDto(
                        Success: true,
                        Message: "Sync queued as background task",
                        SyncStartedAtUtc: DateTime.UtcNow
                    )
                );
            })
                .RequireRateLimiting("media")
                .WithSummary("Queue manual sync of events and device configuration")
                .WithDescription("Initiates background pull of events and config for all devices in the account");

            _ = group.MapGet("/provider-accounts/{id:guid}/config-sync-interval", async (
                Guid id,
                IRingAccountSyncState syncState,
                CancellationToken ct) =>
                Results.Ok(new ConfigSyncIntervalDto((await syncState.GetConfigSyncStateAsync(id, ct)).IntervalHours)))
                .RequireRateLimiting("media")
                .WithSummary("Get the config sync interval")
                .WithDescription("Returns the hours between config syncs for the account. 24 when the account has none saved.");

            _ = group.MapPut("/provider-accounts/{id:guid}/config-sync-interval", async (
                Guid id,
                ConfigSyncIntervalDto request,
                IRingAccountSyncState syncState,
                CancellationToken ct) =>
            {
                if (request.Hours is < MinConfigSyncIntervalHours or > MaxConfigSyncIntervalHours)
                {
                    return Results.BadRequest($"Hours must be between {MinConfigSyncIntervalHours} and {MaxConfigSyncIntervalHours}.");
                }

                return await syncState.SetConfigIntervalHoursAsync(id, request.Hours, ct)
                    ? Results.Ok(request)
                    : Results.NotFound();
            })
                .RequireRateLimiting("media")
                .WithSummary("Set the config sync interval")
                .WithDescription("Saves the hours between config syncs (1 to 720) for the account. 404 when the account has no Ring row.");
        }

        /// <summary>Shortest config sync interval the PUT endpoint accepts, in hours.</summary>
        private const int MinConfigSyncIntervalHours = 1;

        /// <summary>Longest config sync interval the PUT endpoint accepts, in hours (30 days).</summary>
        private const int MaxConfigSyncIntervalHours = 720;

        /// <summary>Largest look-back, in days, a sync-now request may ask for.</summary>
        private const int MaxSyncDays = 3650;

    /// <summary>
    /// Returns the UTC instant for a bound date. System.Text.Json converts an offset timestamp such as "+02:00" to local
    /// time, so relabelling it as UTC would shift the instant by the server's offset. Unspecified dates are taken as UTC.
    /// </summary>
    private static DateTime ToUtcInstant(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>Validates the sync-now body and turns it into a runner request. Dates are treated as UTC.</summary>
    private static bool TryBuildSyncRequest(SyncNowRequest body, out AccountSyncRequest? request, out string? error)
    {
        request = null;
        error = null;

        AccountSyncType type = AccountSyncType.All;
        if (body.SyncType != null && !Enum.TryParse(body.SyncType, ignoreCase: true, out type))
        {
            error = "syncType must be All, Events or Snapshots";
            return false;
        }

        if (!Enum.IsDefined(type))
        {
            error = "syncType must be All, Events or Snapshots";
            return false;
        }

        DateTime? from = null;
        DateTime? to = null;
        bool allHistory = false;

        switch (body.RangeKind?.ToLowerInvariant())
        {
            case null:
                break;
            case "all":
                allHistory = true;
                break;
            case "days":
                if (body.Days is not > 0 or > MaxSyncDays)
                {
                    error = $"days must be between 1 and {MaxSyncDays}";
                    return false;
                }

                from = DateTime.UtcNow.AddDays(-body.Days.Value);
                break;
            case "custom":
                if (body.StartUtc is null)
                {
                    error = "from is required for a custom range";
                    return false;
                }

                from = ToUtcInstant(body.StartUtc.Value);
                to = body.EndUtc is null ? null : ToUtcInstant(body.EndUtc.Value);
                if (to <= from)
                {
                    error = "to must be after from";
                    return false;
                }

                break;
            default:
                error = "rangeKind must be Days, All or Custom";
                return false;
        }

        request = new AccountSyncRequest(type, from, to, allHistory);
        return true;
    }

    /// <summary>
    /// Background first sync for an account. Errors are recorded on the account by <see cref="IAccountSyncRunner"/>;
    /// nothing is rethrown because no caller is waiting on this task.
    /// </summary>
    private static async Task RunInitialSyncAsync(IServiceScopeFactory scopeFactory, Guid accountId)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IAccountSyncRunner runner = scope.ServiceProvider.GetRequiredService<IAccountSyncRunner>();
            await runner.RunInitialAsync(accountId, CancellationToken.None);
        }
        catch
        {
            // The runner records its own failures on the account.
        }
    }
    }

    /// <summary>Request DTO for creating a new user.</summary>
    public record CreateUserRequest(
        /// <summary>The provider's unique identifier for the user.</summary>
        string ProviderUserKey,
        /// <summary>User-friendly display name.</summary>
        string DisplayName,
        /// <summary>User's email address, if available.</summary>
        string? Email
    );

    /// <summary>Request DTO for updating an existing user.</summary>
    public record UpdateUserRequest(
        /// <summary>Updated user-friendly display name.</summary>
        string DisplayName,
        /// <summary>Updated email address, if provided.</summary>
        string? Email
    );

    /// <summary>Request DTO for creating a new provider account link.</summary>
    public record CreateProviderAccountRequest(
        /// <summary>The user to link this provider account to.</summary>
        Guid UserId,
        /// <summary>Name of the provider (e.g., "Ring", "Wyze").</summary>
        string ProviderName
    );

    /// <summary>Request DTO for updating an existing provider account.</summary>
    public record UpdateProviderAccountRequest(
        /// <summary>True to activate the account; false to deactivate.</summary>
        bool IsActive,
        /// <summary>Timestamp of the last successful authentication with the provider, if updated.</summary>
        DateTime? LastSuccessfulAuthUtc = null,
        /// <summary>Timestamp of the last download completion, if updated.</summary>
        DateTime? LastDownloadTimeUtc = null
    );
}
