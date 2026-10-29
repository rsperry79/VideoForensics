using Microsoft.Extensions.DependencyInjection;
using AngleSharp.Dom;

using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The pending-tags badge: hidden when idle, a count, the help text, the list with remove, and the attached notice.</summary>
public class ReviewPendingTags_Tests : ReviewTestContext
{
    private PendingEvidenceTags Pending => Services.GetRequiredService<PendingEvidenceTags>();

    private PendingTag CreateTag(Guid deviceId, CaseItemKind kind = CaseItemKind.Media)
    {
        return new PendingTag(kind, Guid.NewGuid(), deviceId, FixedNowUtc, PendingTagFakes.Reason, "actor-1");
    }

    private IRenderedComponent<ReviewPendingTags> RenderBadge()
    {
        AddDevices(2);
        return Render<ReviewPendingTags>(parameters => parameters
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf)));
    }

    [Fact]
    public void ReviewPendingTags_NothingPendingAndNoAttach_RendersNothing()
    {
        // Arrange and Act
        IRenderedComponent<ReviewPendingTags> cut = RenderBadge();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-pending-tags']"));
    }

    [Fact]
    public void ReviewPendingTags_WithPendingTags_ShowsTheLocalizedCount()
    {
        // Arrange
        AddDevices(1);
        _ = Pending.Add(CreateTag(Devices[0].Id));
        _ = Pending.Add(CreateTag(Devices[0].Id, CaseItemKind.Event));

        // Act
        IRenderedComponent<ReviewPendingTags> cut = RenderBadge();

        // Assert
        Assert.Equal(L["PendingTagsCount", 2].Value, cut.Find("[data-testid='review-pending-tags-count']").TextContent.Trim());
    }

    [Fact]
    public void ReviewPendingTags_Badge_CarriesTheInMemoryHelpText()
    {
        // Arrange
        AddDevices(1);
        _ = Pending.Add(CreateTag(Devices[0].Id));

        // Act
        IRenderedComponent<ReviewPendingTags> cut = RenderBadge();

        // Assert
        IElement open = cut.Find("[data-testid='review-pending-tags-open']");
        Assert.Equal(L["PendingTagsHelp"].Value, open.GetAttribute("title"));
        Assert.False(string.IsNullOrWhiteSpace(L["PendingTagsHelp"].Value));
    }

    [Fact]
    public void ReviewPendingTags_OpenPopup_ListsEachItemWithItsKindAndCamera()
    {
        // Arrange
        AddDevices(1);
        _ = Pending.Add(CreateTag(Devices[0].Id, CaseItemKind.Media));
        _ = Pending.Add(CreateTag(Devices[0].Id, CaseItemKind.Event));
        IRenderedComponent<ReviewPendingTags> cut = RenderBadge();

        // Act
        cut.Find("[data-testid='review-pending-tags-open']").Click();

        // Assert
        IReadOnlyList<IElement> rows = cut.FindAll("[data-testid='review-pending-tag-row']");
        Assert.Equal(2, rows.Count);
        Assert.Contains(L["PendingTagKindMedia"].Value, rows[0].TextContent);
        Assert.Contains(NameOf(Devices[0]), rows[0].TextContent);
        Assert.Contains(L["PendingTagKindEvent"].Value, rows[1].TextContent);
    }

    [Fact]
    public void ReviewPendingTags_RemoveButton_DropsTheItemAndUpdatesTheCount()
    {
        // Arrange
        AddDevices(1);
        _ = Pending.Add(CreateTag(Devices[0].Id));
        _ = Pending.Add(CreateTag(Devices[0].Id));
        IRenderedComponent<ReviewPendingTags> cut = RenderBadge();
        cut.Find("[data-testid='review-pending-tags-open']").Click();

        // Act
        cut.Find("[data-testid='review-pending-tag-remove']").Click();

        // Assert
        Assert.Equal(1, Pending.Count);
        Assert.Equal(L["PendingTagsCount", 1].Value, cut.Find("[data-testid='review-pending-tags-count']").TextContent.Trim());
        Assert.Single(cut.FindAll("[data-testid='review-pending-tag-row']"));
    }

    [Fact]
    public async Task ReviewPendingTags_AfterAttach_ShowsTheNoticeUntilDismissed()
    {
        // Arrange
        AddDevices(1);
        _ = Pending.Add(CreateTag(Devices[0].Id));
        IRenderedComponent<ReviewPendingTags> cut = RenderBadge();
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var caseState = await PendingTagFakes.ActivateAsync(PendingTagFakes.CreateRepository(openCase), openCase);

        // Act
        await cut.InvokeAsync(() => Pending.AttachToActiveCaseAsync(caseState, CancellationToken.None));

        // Assert
        IElement notice = cut.Find("[data-testid='review-pending-tags-attached']");
        Assert.Equal(L["PendingTagsAttached", 1].Value, notice.TextContent.Trim());

        // Act again
        cut.Find("[data-testid='review-pending-tags-dismiss']").Click();

        // Assert again
        Assert.Empty(cut.FindAll("[data-testid='review-pending-tags-attached']"));
    }

    [Fact]
    public void ReviewPendingTags_Dispose_UnsubscribesFromPendingTags()
    {
        // Arrange
        IRenderedComponent<ReviewPendingTags> cut = RenderBadge();
        PendingEvidenceTags pending = Pending;
        Assert.NotNull(GetHandlers(pending, "OnChange"));
        Assert.NotNull(GetHandlers(pending, "Attached"));

        // Act
        ((IDisposable)cut.Instance).Dispose();

        // Assert
        Assert.Null(GetHandlers(pending, "OnChange"));
        Assert.Null(GetHandlers(pending, "Attached"));
    }

    private static Delegate? GetHandlers(PendingEvidenceTags pending, string eventName)
    {
        return (Delegate?)typeof(PendingEvidenceTags)
            .GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(pending);
    }
}
