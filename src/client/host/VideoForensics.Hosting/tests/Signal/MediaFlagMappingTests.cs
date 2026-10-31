using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.Signal;

/// <summary>Media flags survive the trip to the wire DTOs and back, with enums carried as their names.</summary>
public class MediaFlagMappingTests
{
    private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MediaFlag_RoundTrip_PreservesEveryFieldAndEnum()
    {
        var flag = new MediaFlag
        {
            Id = Guid.NewGuid(),
            FlagType = MediaFlagType.BitrateCollapse,
            DeviceId = Guid.NewGuid(),
            MediaItemId = Guid.NewGuid(),
            WindowStartUtc = From,
            WindowEndUtc = From.AddMinutes(5),
            RaisedAtUtc = From.AddMinutes(6),
            Severity = MediaFlagSeverity.Alert,
            Status = MediaFlagStatus.Dismissed,
            ReviewedBy = "operator-1",
            ReviewedAtUtc = From.AddHours(1),
            ReviewNote = "false positive",
            NoticeId = Guid.NewGuid(),
            RowVersion = 3,
        };

        MediaFlag back = flag.ToDto().ToDomain();

        Assert.Equal(flag.Id, back.Id);
        Assert.Equal(flag.FlagType, back.FlagType);
        Assert.Equal(flag.DeviceId, back.DeviceId);
        Assert.Equal(flag.MediaItemId, back.MediaItemId);
        Assert.Equal(flag.WindowStartUtc, back.WindowStartUtc);
        Assert.Equal(flag.WindowEndUtc, back.WindowEndUtc);
        Assert.Equal(flag.RaisedAtUtc, back.RaisedAtUtc);
        Assert.Equal(flag.Severity, back.Severity);
        Assert.Equal(flag.Status, back.Status);
        Assert.Equal(flag.ReviewedBy, back.ReviewedBy);
        Assert.Equal(flag.ReviewedAtUtc, back.ReviewedAtUtc);
        Assert.Equal(flag.ReviewNote, back.ReviewNote);
        Assert.Equal(flag.NoticeId, back.NoticeId);
        Assert.Equal(flag.RowVersion, back.RowVersion);
    }

    [Fact]
    public void MediaFlag_ToDto_WritesEnumsAsNames()
    {
        var flag = new MediaFlag { FlagType = MediaFlagType.RssiDeviation, Severity = MediaFlagSeverity.Warn, Status = MediaFlagStatus.Kept };

        MediaFlagDto dto = flag.ToDto();

        Assert.Equal("RssiDeviation", dto.FlagType);
        Assert.Equal("Warn", dto.Severity);
        Assert.Equal("Kept", dto.Status);
    }

    [Fact]
    public void MediaFlag_RoundTrip_EveryFlagTypeAndStatusSurvives()
    {
        foreach (MediaFlagType type in Enum.GetValues<MediaFlagType>())
        {
            foreach (MediaFlagStatus status in Enum.GetValues<MediaFlagStatus>())
            {
                var flag = new MediaFlag { FlagType = type, Status = status };
                Assert.Equal(type, flag.ToDto().ToDomain().FlagType);
                Assert.Equal(status, flag.ToDto().ToDomain().Status);
            }
        }
    }

    [Fact]
    public void MediaFlag_ToDomain_UnknownStatus_ThrowsArgumentException()
    {
        var dto = new MediaFlagDto(
            Guid.NewGuid(), "BitrateCollapse", Guid.NewGuid(), null, From, From, From, "Warn", "Archived", null, null, null, null, 0);

        _ = Assert.Throws<ArgumentException>(() => dto.ToDomain());
    }

    [Fact]
    public void MediaFlagPage_RoundTrip_KeepsItemsAndTotal()
    {
        var page = new MediaFlagPage([new MediaFlag { FlagType = MediaFlagType.Jamming }], 42);

        MediaFlagPage back = page.ToDto().ToDomain();

        Assert.Equal(42, back.TotalCount);
        Assert.Equal(MediaFlagType.Jamming, Assert.Single(back.Items).FlagType);
    }

    [Fact]
    public void MediaFlagFilter_RoundTrip_PreservesEveryField()
    {
        var filter = new MediaFlagFilter
        {
            Types = [MediaFlagType.BitrateCollapse, MediaFlagType.RssiDeviation],
            Statuses = [MediaFlagStatus.Unreviewed],
            DeviceIds = [Guid.NewGuid()],
            FromUtc = From,
            ToUtc = From.AddDays(7),
        };

        MediaFlagFilter back = filter.ToDto().ToDomain();

        Assert.Equal(filter.Types, back.Types);
        Assert.Equal(filter.Statuses, back.Statuses);
        Assert.Equal(filter.DeviceIds, back.DeviceIds);
        Assert.Equal(filter.FromUtc, back.FromUtc);
        Assert.Equal(filter.ToUtc, back.ToUtc);
    }

    [Fact]
    public void MediaFlagFilter_RoundTrip_NullCollectionsStayNull()
    {
        MediaFlagFilter back = new MediaFlagFilter().ToDto().ToDomain();

        Assert.Null(back.Types);
        Assert.Null(back.Statuses);
        Assert.Null(back.DeviceIds);
    }

    [Fact]
    public void MediaFlagFilterDto_ToDomain_UnknownType_ThrowsArgumentException()
    {
        var dto = new MediaFlagFilterDto(["NotAType"], null, null, null, null);

        _ = Assert.Throws<ArgumentException>(() => dto.ToDomain());
    }

    [Fact]
    public void FlagStatusConflict_RoundTrip_KeepsExpectedActualAndReviewer()
    {
        var reviewedAt = From.AddHours(2);
        var ex = new FlagStatusConflictException(Guid.NewGuid(), MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, "reviewer-7", reviewedAt);

        FlagStatusConflictDto body = ex.ToDto();
        Assert.Equal("Unreviewed", body.ExpectedStatus);
        Assert.Equal("Kept", body.ActualStatus);
        Assert.Equal("reviewer-7", body.ReviewedBy);
        Assert.Equal(reviewedAt, body.ReviewedAtUtc);

        FlagStatusConflictException back = body.ToDomain(ex.FlagId);
        Assert.Equal(ex.FlagId, back.FlagId);
        Assert.Equal(MediaFlagStatus.Unreviewed, back.ExpectedStatus);
        Assert.Equal(MediaFlagStatus.Kept, back.ActualStatus);
        Assert.Equal("reviewer-7", back.ReviewedBy);
        Assert.Equal(reviewedAt, back.ReviewedAtUtc);
    }
}
