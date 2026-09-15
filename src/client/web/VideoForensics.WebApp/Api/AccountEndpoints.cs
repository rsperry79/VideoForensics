using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
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
