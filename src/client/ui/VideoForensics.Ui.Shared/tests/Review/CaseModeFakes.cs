using Moq;

using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Builds an active <see cref="ReviewCaseMode"/> over repository doubles that hold the given pinned rows.</summary>
public static class CaseModeFakes
{
    /// <summary>Enters a new case whose pins are the given media items and events, and returns the active mode.</summary>
    public static async Task<ReviewCaseMode> ActiveAsync(IReadOnlyList<MediaItem> media, IReadOnlyList<Event>? events = null)
    {
        Guid caseId = Guid.NewGuid();
        DateTime added = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var cases = new Mock<ICaseRepository>();
        var mediaRepository = new Mock<IMediaItemRepository>();
        var eventRepository = new Mock<IEventRepository>();

        var pins = new List<CaseItem>();
        foreach (MediaItem item in media)
        {
            pins.Add(new CaseItem { Id = Guid.NewGuid(), CaseId = caseId, Kind = CaseItemKind.Media, MediaItemId = item.Id, Reason = "r", AddedBy = "op", AddedAtUtc = added });
            _ = mediaRepository.Setup(r => r.GetAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        }

        foreach (Event evt in events ?? [])
        {
            pins.Add(new CaseItem { Id = Guid.NewGuid(), CaseId = caseId, Kind = CaseItemKind.Event, EventId = evt.Id, Reason = "r", AddedBy = "op", AddedAtUtc = added });
            _ = eventRepository.Setup(r => r.GetAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);
        }

        _ = cases
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForensicCase { Id = caseId, CaseNumber = "CASE-1", Title = "Case", CreatedBy = "op" });
        _ = cases
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);

        var mode = new ReviewCaseMode(cases.Object, mediaRepository.Object, eventRepository.Object);
        _ = await mode.EnterAsync(caseId, CancellationToken.None);
        return mode;
    }
}
