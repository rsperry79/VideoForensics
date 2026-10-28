using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.Core.Utilities;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    public class DownloadJobEndpointsTests
    {
        private static readonly Guid OperatorId = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

        [Fact]
        public async Task DownloadJobEndpoints_Create_UnknownAccount_ReturnsNotFound()
        {
            var accounts = new Mock<IProviderAccountRepository>();
            accounts.Setup(a => a.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccount?)null);

            var result = await DownloadJobEndpoints.CreateAsync(ValidRequest(), OperatorContext(), accounts.Object, new Mock<IDownloadJobRepository>().Object, CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }

        [Fact]
        public async Task DownloadJobEndpoints_Create_RetiredAccount_ReturnsNotFound()
        {
            var accounts = new Mock<IProviderAccountRepository>();
            accounts.Setup(a => a.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Account(removed: true));

            var result = await DownloadJobEndpoints.CreateAsync(ValidRequest(), OperatorContext(), accounts.Object, new Mock<IDownloadJobRepository>().Object, CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }

        [Fact]
        public async Task DownloadJobEndpoints_Create_UnknownKind_ReturnsBadRequest()
        {
            var accounts = AccountsReturning();
            var request = ValidRequest() with { Kind = "Everything" };

            var result = await DownloadJobEndpoints.CreateAsync(request, OperatorContext(), accounts.Object, new Mock<IDownloadJobRepository>().Object, CancellationToken.None);

            Assert.IsType<BadRequest>(result.Result);
        }

        [Fact]
        public async Task DownloadJobEndpoints_Create_ReversedRange_ReturnsBadRequest()
        {
            var accounts = AccountsReturning();
            var request = ValidRequest() with { StartDateUtc = DateTime.UtcNow, EndDateUtc = DateTime.UtcNow.AddDays(-1) };

            var result = await DownloadJobEndpoints.CreateAsync(request, OperatorContext(), accounts.Object, new Mock<IDownloadJobRepository>().Object, CancellationToken.None);

            Assert.IsType<BadRequest>(result.Result);
        }

        [Fact]
        public async Task DownloadJobEndpoints_Create_RangeOverAYear_ReturnsBadRequest()
        {
            var accounts = AccountsReturning();
            var request = ValidRequest() with { StartDateUtc = DateTime.UtcNow.AddDays(-400), EndDateUtc = DateTime.UtcNow };

            var result = await DownloadJobEndpoints.CreateAsync(request, OperatorContext(), accounts.Object, new Mock<IDownloadJobRepository>().Object, CancellationToken.None);

            Assert.IsType<BadRequest>(result.Result);
        }

        [Fact]
        public async Task DownloadJobEndpoints_Create_OutputOutsideDownloadRoot_ReturnsBadRequest()
        {
            var accounts = AccountsReturning();
            var request = ValidRequest() with { OutputPath = @"..\..\Windows" };

            var result = await DownloadJobEndpoints.CreateAsync(request, OperatorContext(), accounts.Object, new Mock<IDownloadJobRepository>().Object, CancellationToken.None);

            Assert.IsType<BadRequest>(result.Result);
        }

        [Fact]
        public async Task DownloadJobEndpoints_Create_ValidRequest_QueuesJobForAccountUnderDownloadRoot()
        {
            var accounts = AccountsReturning();
            var jobs = new Mock<IDownloadJobRepository>();
            DownloadJob? queued = null;
            jobs.Setup(j => j.EnqueueAsync(It.IsAny<DownloadJob>(), It.IsAny<CancellationToken>()))
                .Callback<DownloadJob, CancellationToken>((job, _) => queued = job)
                .Returns(Task.CompletedTask);

            var result = await DownloadJobEndpoints.CreateAsync(ValidRequest(), OperatorContext(), accounts.Object, jobs.Object, CancellationToken.None);

            Assert.IsType<Accepted<DownloadJobDto>>(result.Result);
            Assert.NotNull(queued);
            Assert.Equal(AccountId, queued.ProviderAccountId);
            Assert.Equal(DownloadJobKind.Videos, queued.Kind);
            Assert.Equal(OperatorId, queued.RequestedBy);
            Assert.StartsWith(Path.GetFullPath(PathUtilities.GetDefaultDownloadLocation()), queued.OutputPath);
        }

        [Fact]
        public async Task DownloadJobEndpoints_Get_Missing_ReturnsNotFound()
        {
            var jobs = new Mock<IDownloadJobRepository>();
            jobs.Setup(j => j.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((DownloadJob?)null);

            var result = await DownloadJobEndpoints.GetAsync(Guid.NewGuid(), jobs.Object, CancellationToken.None);

            Assert.IsType<NotFound>(result.Result);
        }

        [Fact]
        public async Task DownloadJobEndpoints_List_TakeOutOfRange_ReturnsBadRequest()
        {
            var jobs = new Mock<IDownloadJobRepository>();

            var result = await DownloadJobEndpoints.ListAsync(AccountId, take: 501, jobs.Object, CancellationToken.None);

            Assert.IsType<BadRequest>(result.Result);
            jobs.Verify(j => j.ListForAccountAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DownloadJobEndpoints_Cancel_AlreadyFinished_ReturnsConflict()
        {
            DownloadJob job = BuildJob(DownloadJobState.Completed);
            var jobs = new Mock<IDownloadJobRepository>();
            jobs.Setup(j => j.GetAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
            jobs.Setup(j => j.CancelAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var result = await DownloadJobEndpoints.CancelAsync(job.Id, jobs.Object, CancellationToken.None);

            Assert.IsType<Conflict>(result.Result);
        }

        private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

        private static CreateDownloadJobRequestDto ValidRequest()
        {
            return new CreateDownloadJobRequestDto(
                AccountId,
                Kind: "Videos",
                StartDateUtc: DateTime.UtcNow.AddDays(-2),
                EndDateUtc: DateTime.UtcNow,
                Force: false,
                OutputPath: "Export");
        }

        private static HttpContext OperatorContext()
        {
            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(VideoForensicsClaimTypes.OperatorId, OperatorId.ToString()) }, "test")),
            };
        }

        private static ProviderAccount Account(bool removed)
        {
            return new ProviderAccount
            {
                Id = AccountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                RemovedUtc = removed ? DateTime.UtcNow : null,
            };
        }

        private static Mock<IProviderAccountRepository> AccountsReturning()
        {
            var accounts = new Mock<IProviderAccountRepository>();
            accounts.Setup(a => a.GetAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(removed: false));
            return accounts;
        }

        private static DownloadJob BuildJob(DownloadJobState state)
        {
            return new DownloadJob
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = AccountId,
                Kind = DownloadJobKind.Videos,
                StartDateUtc = DateTime.UtcNow.AddDays(-1),
                EndDateUtc = DateTime.UtcNow,
                State = state,
                CreatedUtc = DateTime.UtcNow,
                RequestedBy = OperatorId,
            };
        }
    }
}
