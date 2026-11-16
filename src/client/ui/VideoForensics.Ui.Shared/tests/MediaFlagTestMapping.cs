using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// Test-only entity-to-DTO mapping for the media flag repository fakes. Mirrors the server-side
/// VideoForensics.Client.Hosting.Signal.MediaFlagMapping, which this test project does not reference.
/// </summary>
internal static class MediaFlagTestMapping
{
    public static MediaFlagDto ToDto(this MediaFlag flag)
    {
        return new MediaFlagDto(
            flag.Id,
            flag.FlagType.ToString(),
            flag.DeviceId,
            flag.MediaItemId,
            flag.WindowStartUtc,
            flag.WindowEndUtc,
            flag.RaisedAtUtc,
            flag.Severity.ToString(),
            flag.Status.ToString(),
            flag.ReviewedBy,
            flag.ReviewedAtUtc,
            flag.ReviewNote,
            flag.NoticeId,
            flag.RowVersion,
            flag.EventType,
            flag.DeviationSigma);
    }

    public static MediaFlagPageDto ToDto(this MediaFlagPage page)
    {
        return new MediaFlagPageDto(page.Items.Select(f => f.ToDto()).ToList(), page.TotalCount);
    }

    public static MediaFlagFilterDto ToDto(this MediaFlagFilter filter)
    {
        return new MediaFlagFilterDto(
            filter.Types?.Select(t => t.ToString()).ToList(),
            filter.Statuses?.Select(s => s.ToString()).ToList(),
            filter.DeviceIds?.ToList(),
            filter.FromUtc,
            filter.ToUtc);
    }
}
