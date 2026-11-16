using VideoForensics.Core.Api.Contracts.Repositories;using VideoForensics.Core.Api.Contracts;
using Moq;

using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>Builds an active <see cref="ReviewCaseMode"/> over repository doubles that hold the given pinned rows.</summary>
public static class CaseModeFakes
{
    /// <summary>Enters a new case whose pins are the given media items and events, and returns the active mode.</summary>
    public static async Task<ReviewCaseMode> ActiveAsync(IReadOnlyList<MediaItem> media, IReadOnlyList<EventDto>? events = null)
    {
        var caseId = Guid.NewGuid();
        DateTime added = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        var cases = new Mock<ICaseRepository>();
        var mediaRepository = new Mock<IMediaItemRepository>();
        var eventRepository = new Mock<IEventRepository>();

        var pins = new List<CaseItemDto>();
        foreach (MediaItem item in media)
        {
            pins.Add(new CaseItemDto(Guid.NewGuid(), caseId, CaseItemKindDto.Media.ToString(), item.Id, "r", "op", added, null, null, null, null));
            _ = mediaRepository.Setup(r => r.GetAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item.ToDto());
        }

        foreach (EventDto evt in events ?? [])
        {
            pins.Add(new CaseItemDto(Guid.NewGuid(), caseId, CaseItemKindDto.Event.ToString(), evt.Id, "r", "op", added, null, null, null, null));
            _ = eventRepository.Setup(r => r.GetAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);
        }

        _ = cases
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForensicCaseDto(caseId, "CASE-1", "Case", null, CaseStatusDto.Open.ToString(), null, "op", DateTime.UtcNow, DateTime.UtcNow, null, null, null, null, []));
        _ = cases
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);

        var mode = new ReviewCaseMode(cases.Object, mediaRepository.Object, eventRepository.Object);
        _ = await mode.EnterAsync(caseId, CancellationToken.None);
        return mode;
    }
}
