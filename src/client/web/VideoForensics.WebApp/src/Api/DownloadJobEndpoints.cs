using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Download job API. A job is queued here and run by the server's DownloadJobRunner, which is not tied to any page
    /// or client. Callers poll GET /download-jobs/{id} for state and counters.
    /// </summary>
    public static class DownloadJobEndpoints
    {
        // A longer range would hold one job for days and make progress and failure hard to read.
        private static readonly TimeSpan MaxRange = TimeSpan.FromDays(366);
        private const int DefaultTake = 50;
        private const int MaxTake = 500;

        public static void MapDownloadJobEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/download-jobs").RequireAuthorization();

            _ = group.MapPost("/", CreateAsync)
                .RequireRateLimiting("media")
                .WithSummary("Queue a download job")
                .WithDescription("Queues a download for one account and range. Returns 202 with the job; the server runs it in the background.");

            _ = group.MapGet("/{id:guid}", GetAsync)
                .WithSummary("Get a download job");

            _ = group.MapGet("/", ListAsync)
                .WithSummary("List an account's download jobs, newest first");

            _ = group.MapPost("/{id:guid}/cancel", CancelAsync)
                .WithSummary("Cancel a queued or running download job");
        }

        public static async Task<Results<Accepted<DownloadJobDto>, BadRequest, NotFound, UnauthorizedHttpResult>> CreateAsync(
            CreateDownloadJobRequestDto request,
            HttpContext context,
            IProviderAccountRepository accounts,
            IDownloadJobRepository jobs,
            CancellationToken ct)
        {
            if (!Enum.TryParse(request.Kind, ignoreCase: true, out DownloadJobKind kind) || !Enum.IsDefined(kind))
            {
                return TypedResults.BadRequest();
            }

            if (request.EndDateUtc < request.StartDateUtc || request.EndDateUtc - request.StartDateUtc > MaxRange)
            {
                return TypedResults.BadRequest();
            }

            if (!DownloadEndpoints.TryResolveSafeOutputPath(request.OutputPath, out string outputPath))
            {
                return TypedResults.BadRequest();
            }

            if (!TryGetOperatorId(context, out Guid operatorId))
            {
                return TypedResults.Unauthorized();
            }

            ProviderAccount? account = await accounts.GetAsync(request.AccountId, ct);
            if (account is null || account.RemovedUtc.HasValue)
            {
                return TypedResults.NotFound();
            }

            var job = new DownloadJob
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = account.Id,
                Kind = kind,
                StartDateUtc = request.StartDateUtc,
                EndDateUtc = request.EndDateUtc,
                Force = request.Force,
                OutputPath = outputPath,
                State = DownloadJobState.Queued,
                CreatedUtc = DateTime.UtcNow,
                RequestedBy = operatorId,
            };
            await jobs.EnqueueAsync(job, ct);
            return TypedResults.Accepted($"/api/v1/download-jobs/{job.Id}", job.ToDto());
        }

        public static async Task<Results<Ok<DownloadJobDto>, NotFound>> GetAsync(Guid id, IDownloadJobRepository jobs, CancellationToken ct)
        {
            DownloadJob? job = await jobs.GetAsync(id, ct);
            return job is null ? TypedResults.NotFound() : TypedResults.Ok(job.ToDto());
        }

        public static async Task<Results<Ok<IReadOnlyList<DownloadJobDto>>, BadRequest>> ListAsync(
            Guid accountId,
            int? take,
            IDownloadJobRepository jobs,
            CancellationToken ct)
        {
            int effectiveTake = take ?? DefaultTake;
            if (effectiveTake is < 1 or > MaxTake)
            {
                return TypedResults.BadRequest();
            }

            IReadOnlyList<DownloadJob> list = await jobs.ListForAccountAsync(accountId, effectiveTake, ct);
            return TypedResults.Ok<IReadOnlyList<DownloadJobDto>>(list.Select(j => j.ToDto()).ToList());
        }

        public static async Task<Results<Ok<DownloadJobDto>, NotFound, Conflict>> CancelAsync(Guid id, IDownloadJobRepository jobs, CancellationToken ct)
        {
            DownloadJob? job = await jobs.GetAsync(id, ct);
            if (job is null)
            {
                return TypedResults.NotFound();
            }

            if (!await jobs.CancelAsync(id, ct))
            {
                // Already finished, so there is nothing to cancel.
                return TypedResults.Conflict();
            }

            DownloadJob? cancelled = await jobs.GetAsync(id, ct);
            return TypedResults.Ok((cancelled ?? job).ToDto());
        }

        private static bool TryGetOperatorId(HttpContext context, out Guid operatorId)
        {
            string? claim = context.User.FindFirst(VideoForensicsClaimTypes.OperatorId)?.Value;
            return Guid.TryParse(claim, out operatorId);
        }
    }

    /// <summary>Maps the download job entity to its API shape.</summary>
    internal static class DownloadJobMapping
    {
        public static DownloadJobDto ToDto(this DownloadJob job)
        {
            return new DownloadJobDto(
                Id: job.Id,
                ProviderAccountId: job.ProviderAccountId,
                Kind: job.Kind.ToString(),
                StartDateUtc: job.StartDateUtc,
                EndDateUtc: job.EndDateUtc,
                Force: job.Force,
                OutputPath: job.OutputPath,
                State: job.State.ToString(),
                CreatedUtc: job.CreatedUtc,
                StartedUtc: job.StartedUtc,
                FinishedUtc: job.FinishedUtc,
                FilesDownloaded: job.FilesDownloaded,
                FilesSkipped: job.FilesSkipped,
                FilesFailed: job.FilesFailed,
                ErrorMessage: job.ErrorMessage);
        }
    }
}
