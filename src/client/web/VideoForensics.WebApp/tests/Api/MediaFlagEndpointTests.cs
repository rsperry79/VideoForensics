using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>The media-flag endpoints validate at the boundary, map repository failures to status codes and publish every change.</summary>
    public class MediaFlagEndpointTests
    {
        private static readonly Guid FlagId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid DeviceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OperatorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

        private static MediaFlagQueryParameters NoFilter(int? skip = null, int? take = null, string? direction = null)
        {
            return new MediaFlagQueryParameters(null, null, null, null, null, skip, take, direction);
        }

        private static HttpContext Context(Guid? operatorId)
        {
            var claims = new List<Claim>();
            if (operatorId.HasValue)
            {
                claims.Add(new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.Value.ToString()));
            }

            return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        }

        private static MediaFlag Flag(MediaFlagStatus status = MediaFlagStatus.Unreviewed)
        {
            return new MediaFlag
            {
                Id = FlagId,
                DeviceId = DeviceId,
                FlagType = MediaFlagType.BitrateCollapse,
                WindowStartUtc = From,
                WindowEndUtc = From.AddMinutes(5),
                RaisedAtUtc = From,
                Status = status,
            };
        }

        private static int StatusOf(IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? 200;

        [Fact]
        public async Task ListAsync_ValidQuery_PassesFilterAndPagingAndReturnsPage()
        {
            var flags = new Mock<IMediaFlagRepository>();
            _ = flags.Setup(f => f.ListPageAsync(It.IsAny<MediaFlagFilter>(), 20, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaFlagPage([Flag()], 5));
            var query = new MediaFlagQueryParameters("BitrateCollapse", "Unreviewed", DeviceId.ToString("D"), From, From.AddDays(7), 20, 10, null);

            IResult result = await MediaFlagEndpoints.ListAsync(query, flags.Object, CancellationToken.None);

            Ok<MediaFlagPageDto> ok = Assert.IsType<Ok<MediaFlagPageDto>>(result);
            Assert.Equal(5, ok.Value!.TotalCount);
            Assert.Equal(FlagId, Assert.Single(ok.Value.Items).Id);
            flags.Verify(f => f.ListPageAsync(
                It.Is<MediaFlagFilter>(x => x.Types!.Single() == MediaFlagType.BitrateCollapse
                    && x.Statuses!.Single() == MediaFlagStatus.Unreviewed
                    && x.DeviceIds!.Single() == DeviceId
                    && x.FromUtc == From && x.ToUtc == From.AddDays(7)),
                20, 10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ListAsync_UnknownFlagType_ReturnsBadRequestWithoutQuerying()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var query = new MediaFlagQueryParameters("NotAType", null, null, null, null, null, null, null);

            IResult result = await MediaFlagEndpoints.ListAsync(query, flags.Object, CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            flags.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ListAsync_FromAfterTo_ReturnsBadRequest()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var query = new MediaFlagQueryParameters(null, null, null, From.AddDays(1), From, null, null, null);

            IResult result = await MediaFlagEndpoints.ListAsync(query, flags.Object, CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            flags.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ListAsync_TakeOutOfRange_ReturnsBadRequest()
        {
            var flags = new Mock<IMediaFlagRepository>();

            IResult result = await MediaFlagEndpoints.ListAsync(NoFilter(take: 0), flags.Object, CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            flags.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CountAsync_ValidFilter_ReturnsCount()
        {
            var flags = new Mock<IMediaFlagRepository>();
            _ = flags.Setup(f => f.CountAsync(It.IsAny<MediaFlagFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(9);

            IResult result = await MediaFlagEndpoints.CountAsync(NoFilter(), flags.Object, CancellationToken.None);

            Ok<int> ok = Assert.IsType<Ok<int>>(result);
            Assert.Equal(9, ok.Value);
        }

        [Fact]
        public async Task GetAsync_Missing_ReturnsNotFound()
        {
            var flags = new Mock<IMediaFlagRepository>();
            _ = flags.Setup(f => f.GetAsync(FlagId, It.IsAny<CancellationToken>())).ReturnsAsync((MediaFlag?)null);

            IResult result = await MediaFlagEndpoints.GetAsync(FlagId, flags.Object, CancellationToken.None);

            Assert.Equal(404, StatusOf(result));
        }

        [Fact]
        public async Task GetAsync_Found_ReturnsDto()
        {
            var flags = new Mock<IMediaFlagRepository>();
            _ = flags.Setup(f => f.GetAsync(FlagId, It.IsAny<CancellationToken>())).ReturnsAsync(Flag());

            IResult result = await MediaFlagEndpoints.GetAsync(FlagId, flags.Object, CancellationToken.None);

            Ok<MediaFlagDto> ok = Assert.IsType<Ok<MediaFlagDto>>(result);
            Assert.Equal("BitrateCollapse", ok.Value!.FlagType);
            Assert.Equal("Unreviewed", ok.Value.Status);
        }

        [Fact]
        public async Task PositionAsync_FlagInFilter_ReturnsOkWithPosition()
        {
            var flags = new Mock<IMediaFlagRepository>();
            _ = flags.Setup(f => f.GetPositionAsync(FlagId, It.IsAny<MediaFlagFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(3);

            IResult result = await MediaFlagEndpoints.PositionAsync(FlagId, NoFilter(), flags.Object, CancellationToken.None);

            Ok<int> ok = Assert.IsType<Ok<int>>(result);
            Assert.Equal(3, ok.Value);
        }

        [Fact]
        public async Task PositionAsync_FlagNotInFilter_ReturnsNotFound()
        {
            var flags = new Mock<IMediaFlagRepository>();
            _ = flags.Setup(f => f.GetPositionAsync(FlagId, It.IsAny<MediaFlagFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

            IResult result = await MediaFlagEndpoints.PositionAsync(FlagId, NoFilter(), flags.Object, CancellationToken.None);

            Assert.Equal(404, StatusOf(result));
        }

        [Fact]
        public async Task NeighbourAsync_AtEndOfList_ReturnsNoContent()
        {
            var flags = new Mock<IMediaFlagRepository>();
            _ = flags.Setup(f => f.GetAsync(FlagId, It.IsAny<CancellationToken>())).ReturnsAsync(Flag());
            _ = flags.Setup(f => f.GetNeighbourAsync(FlagId, It.IsAny<MediaFlagFilter>(), MediaFlagNeighbour.Next, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaFlag?)null);

            IResult result = await MediaFlagEndpoints.NeighbourAsync(FlagId, NoFilter(direction: "Next"), flags.Object, CancellationToken.None);

            Assert.Equal(204, StatusOf(result));
        }

        [Fact]
        public async Task NeighbourAsync_UnknownDirection_ReturnsBadRequest()
        {
            var flags = new Mock<IMediaFlagRepository>();

            IResult result = await MediaFlagEndpoints.NeighbourAsync(FlagId, NoFilter(direction: "Sideways"), flags.Object, CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            flags.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NeighbourAsync_FlagMissing_ReturnsNotFound()
        {
            var flags = new Mock<IMediaFlagRepository>();
            _ = flags.Setup(f => f.GetAsync(FlagId, It.IsAny<CancellationToken>())).ReturnsAsync((MediaFlag?)null);

            IResult result = await MediaFlagEndpoints.NeighbourAsync(FlagId, NoFilter(direction: "Previous"), flags.Object, CancellationToken.None);

            Assert.Equal(404, StatusOf(result));
        }

        [Fact]
        public async Task SetStatusAsync_Success_ReturnsOkAndPublishesChange()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            _ = flags.Setup(f => f.UpdateStatusAsync(FlagId, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, "note", OperatorId.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Flag(MediaFlagStatus.Kept));

            IResult result = await MediaFlagEndpoints.SetStatusAsync(
                FlagId, new SetFlagStatusRequestDto("Unreviewed", "Kept", "note"), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            Ok<MediaFlagDto> ok = Assert.IsType<Ok<MediaFlagDto>>(result);
            Assert.Equal("Kept", ok.Value!.Status);
            notifier.Verify(n => n.MediaFlagChangedAsync(FlagId, DeviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetStatusAsync_OperatorComesFromTokenNotBody()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            _ = flags.Setup(f => f.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagStatus>(), It.IsAny<MediaFlagStatus>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Flag(MediaFlagStatus.Kept));

            // The request DTO has no operator field, so a body cannot name the reviewer; the token's operator is what the repository receives.
            _ = await MediaFlagEndpoints.SetStatusAsync(
                FlagId, new SetFlagStatusRequestDto("Unreviewed", "Kept", null), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            flags.Verify(f => f.UpdateStatusAsync(FlagId, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, OperatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetStatusAsync_Conflict_Returns409WithBodyAndDoesNotPublish()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            var reviewedAt = From.AddHours(2);
            _ = flags.Setup(f => f.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagStatus>(), It.IsAny<MediaFlagStatus>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FlagStatusConflictException(FlagId, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "reviewer-2", reviewedAt));

            IResult result = await MediaFlagEndpoints.SetStatusAsync(
                FlagId, new SetFlagStatusRequestDto("Unreviewed", "Kept", null), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            Conflict<FlagStatusConflictDto> conflict = Assert.IsType<Conflict<FlagStatusConflictDto>>(result);
            Assert.Equal("Unreviewed", conflict.Value!.ExpectedStatus);
            Assert.Equal("Dismissed", conflict.Value.ActualStatus);
            Assert.Equal("reviewer-2", conflict.Value.ReviewedBy);
            Assert.Equal(reviewedAt, conflict.Value.ReviewedAtUtc);
            notifier.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetStatusAsync_UnknownStatus_ReturnsBadRequestWithoutCalling()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();

            IResult result = await MediaFlagEndpoints.SetStatusAsync(
                FlagId, new SetFlagStatusRequestDto("Unreviewed", "Archived", null), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            flags.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetStatusAsync_ToUnreviewed_ReturnsBadRequestAndPointsAtReopen()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();

            IResult result = await MediaFlagEndpoints.SetStatusAsync(
                FlagId, new SetFlagStatusRequestDto("Dismissed", "Unreviewed", null), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            flags.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetStatusAsync_FlagMissing_ReturnsNotFound()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            _ = flags.Setup(f => f.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagStatus>(), It.IsAny<MediaFlagStatus>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException());

            IResult result = await MediaFlagEndpoints.SetStatusAsync(
                FlagId, new SetFlagStatusRequestDto("Unreviewed", "Kept", null), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            Assert.Equal(404, StatusOf(result));
            notifier.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetStatusAsync_DismissWithoutReason_ReturnsBadRequest()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            _ = flags.Setup(f => f.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MediaFlagStatus>(), It.IsAny<MediaFlagStatus>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Reason is required to dismiss a flag."));

            IResult result = await MediaFlagEndpoints.SetStatusAsync(
                FlagId, new SetFlagStatusRequestDto("Unreviewed", "Dismissed", null), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            notifier.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetStatusAsync_NoOperatorClaim_ReturnsUnauthorized()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();

            IResult result = await MediaFlagEndpoints.SetStatusAsync(
                FlagId, new SetFlagStatusRequestDto("Unreviewed", "Kept", null), flags.Object, notifier.Object, Context(null), CancellationToken.None);

            Assert.Equal(401, StatusOf(result));
            flags.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReopenAsync_Success_ReturnsOkAndPublishesChange()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            _ = flags.Setup(f => f.ReopenAsync(FlagId, OperatorId.ToString(), "new evidence", It.IsAny<CancellationToken>())).ReturnsAsync(Flag());

            IResult result = await MediaFlagEndpoints.ReopenAsync(
                FlagId, new ReopenFlagRequestDto("new evidence"), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            _ = Assert.IsType<Ok<MediaFlagDto>>(result);
            notifier.Verify(n => n.MediaFlagChangedAsync(FlagId, DeviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReopenAsync_NotDismissed_Returns409AndDoesNotPublish()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            _ = flags.Setup(f => f.ReopenAsync(FlagId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new FlagStatusConflictException(FlagId, MediaFlagStatus.Dismissed, MediaFlagStatus.Kept, null, null));

            IResult result = await MediaFlagEndpoints.ReopenAsync(
                FlagId, new ReopenFlagRequestDto("reason"), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            Conflict<FlagStatusConflictDto> conflict = Assert.IsType<Conflict<FlagStatusConflictDto>>(result);
            Assert.Equal("Kept", conflict.Value!.ActualStatus);
            notifier.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ReopenAsync_EmptyReason_ReturnsBadRequest()
        {
            var flags = new Mock<IMediaFlagRepository>();
            var notifier = new Mock<IMediaFlagChangeNotifier>();
            _ = flags.Setup(f => f.ReopenAsync(FlagId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Reason is required."));

            IResult result = await MediaFlagEndpoints.ReopenAsync(
                FlagId, new ReopenFlagRequestDto(""), flags.Object, notifier.Object, Context(OperatorId), CancellationToken.None);

            Assert.Equal(400, StatusOf(result));
            notifier.VerifyNoOtherCalls();
        }
    }
}
