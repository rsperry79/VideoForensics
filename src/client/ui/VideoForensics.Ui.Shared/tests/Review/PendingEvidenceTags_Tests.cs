using Moq;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The in-memory pending evidence tags: add and remove, change notices, concurrency, and attaching to an open case.</summary>
public class PendingEvidenceTags_Tests
{
    private static PendingTag CreateTag(Guid? targetId = null, CaseItemKind kind = CaseItemKind.Media)
    {
        return new PendingTag(
            kind,
            targetId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            PendingTagFakes.NowUtc,
            PendingTagFakes.Reason,
            "actor-1");
    }

    private static PendingEvidenceTags CreateTags()
    {
        return new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
    }

    [Fact]
    public void PendingEvidenceTags_Add_NewItem_ReturnsTrueAndListsTheItem()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        PendingTag tag = CreateTag();

        // Act
        bool added = tags.Add(tag);

        // Assert
        Assert.True(added);
        Assert.Equal(1, tags.Count);
        Assert.Equal(tag, Assert.Single(tags.Items));
    }

    [Fact]
    public void PendingEvidenceTags_Add_SameKindAndTarget_ReturnsFalseAndKeepsOneItem()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        Guid targetId = Guid.NewGuid();
        _ = tags.Add(CreateTag(targetId));

        // Act
        bool added = tags.Add(CreateTag(targetId));

        // Assert
        Assert.False(added);
        Assert.Equal(1, tags.Count);
    }

    [Fact]
    public void PendingEvidenceTags_Add_SameTargetDifferentKind_IsAccepted()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        Guid targetId = Guid.NewGuid();
        _ = tags.Add(CreateTag(targetId, CaseItemKind.Media));

        // Act
        bool added = tags.Add(CreateTag(targetId, CaseItemKind.Event));

        // Assert
        Assert.True(added);
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void PendingEvidenceTags_Remove_PendingItem_ReturnsTrueAndDropsIt()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        PendingTag tag = CreateTag();
        _ = tags.Add(tag);

        // Act
        bool removed = tags.Remove(tag.Kind, tag.TargetId);

        // Assert
        Assert.True(removed);
        Assert.Empty(tags.Items);
    }

    [Fact]
    public void PendingEvidenceTags_Remove_UnknownItem_ReturnsFalseAndChangesNothing()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        _ = tags.Add(CreateTag());

        // Act
        bool removed = tags.Remove(CaseItemKind.Media, Guid.NewGuid());

        // Assert
        Assert.False(removed);
        Assert.Equal(1, tags.Count);
    }

    [Fact]
    public void PendingEvidenceTags_Clear_RemovesEveryItem()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        _ = tags.Add(CreateTag());
        _ = tags.Add(CreateTag());

        // Act
        tags.Clear();

        // Assert
        Assert.Equal(0, tags.Count);
    }

    [Fact]
    public void PendingEvidenceTags_OnChange_RaisedOnceForEachRealChangeOnly()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        int changes = 0;
        tags.OnChange += () => changes++;
        Guid targetId = Guid.NewGuid();

        // Act and Assert
        _ = tags.Add(CreateTag(targetId));
        Assert.Equal(1, changes);

        _ = tags.Add(CreateTag(targetId));
        Assert.Equal(1, changes);

        _ = tags.Remove(CaseItemKind.Media, Guid.NewGuid());
        Assert.Equal(1, changes);

        _ = tags.Remove(CaseItemKind.Media, targetId);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task PendingEvidenceTags_Add_ConcurrentAdds_DoNotLoseItems()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        const int count = 500;

        // Act
        await Task.WhenAll(Enumerable.Range(0, count).Select(_ => Task.Run(() => tags.Add(CreateTag()))));

        // Assert
        Assert.Equal(count, tags.Count);
    }

    [Fact]
    public async Task PendingEvidenceTags_AttachToActiveCaseAsync_WithNoActiveCase_ReturnsZeroAndKeepsItems()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        _ = tags.Add(CreateTag());
        var caseState = PendingTagFakes.CreateUnactivated(PendingTagFakes.CreateRepository(PendingTagFakes.CreateOpenCase()));

        // Act
        AttachResult result = await tags.AttachToActiveCaseAsync(caseState, CancellationToken.None);

        // Assert
        Assert.Equal(new AttachResult(0, 0), result);
        Assert.Equal(1, tags.Count);
    }

    [Fact]
    public async Task PendingEvidenceTags_AttachToActiveCaseAsync_PinsInOrderAndClearsPinnedItems()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        PendingTag first = CreateTag();
        PendingTag second = CreateTag(kind: CaseItemKind.Event);
        PendingTag third = CreateTag();
        _ = tags.Add(first);
        _ = tags.Add(second);
        _ = tags.Add(third);
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var pinned = new List<Guid>();
        var repository = PendingTagFakes.CreateRepository(openCase, pinnedTargets: pinned);
        var caseState = await PendingTagFakes.ActivateAsync(repository, openCase);

        // Act
        AttachResult result = await tags.AttachToActiveCaseAsync(caseState, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Attached);
        Assert.Equal(0, result.Failed);
        Assert.Equal(new[] { first.TargetId, second.TargetId, third.TargetId }, pinned);
        Assert.Empty(tags.Items);
    }

    [Fact]
    public async Task PendingEvidenceTags_AttachToActiveCaseAsync_WhenOnePinFails_KeepsThatItemAndAttachesTheRest()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        PendingTag ok = CreateTag();
        PendingTag refused = CreateTag();
        PendingTag alsoOk = CreateTag();
        _ = tags.Add(ok);
        _ = tags.Add(refused);
        _ = tags.Add(alsoOk);
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var repository = PendingTagFakes.CreateRepository(openCase, failingTargets: new HashSet<Guid> { refused.TargetId });
        var caseState = await PendingTagFakes.ActivateAsync(repository, openCase);

        // Act
        AttachResult result = await tags.AttachToActiveCaseAsync(caseState, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Attached);
        Assert.Equal(1, result.Failed);
        PendingTag kept = Assert.Single(tags.Items);
        Assert.Equal(refused.TargetId, kept.TargetId);
    }

    [Fact]
    public async Task PendingEvidenceTags_AttachToActiveCaseAsync_ItemAlreadyInCase_CountsAsAttachedAndIsCleared()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        PendingTag tag = CreateTag();
        _ = tags.Add(tag);
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var existing = new CaseItem
        {
            Id = Guid.NewGuid(),
            CaseId = openCase.Id,
            Kind = CaseItemKind.Media,
            MediaItemId = tag.TargetId,
            Reason = "earlier",
            AddedBy = "actor-0",
        };
        var repository = PendingTagFakes.CreateRepository(openCase, existingItems: [existing]);
        var caseState = await PendingTagFakes.ActivateAsync(repository, openCase);

        // Act
        AttachResult result = await tags.AttachToActiveCaseAsync(caseState, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.Attached);
        Assert.Equal(0, result.Failed);
        Assert.Empty(tags.Items);
        repository.Verify(r => r.AddItemAsync(
            It.IsAny<Guid>(), It.IsAny<CaseItemKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PendingEvidenceTags_AttachToActiveCaseAsync_WhenItemsAttach_RaisesOnChangeOnceAndAttachedEvent()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        _ = tags.Add(CreateTag());
        _ = tags.Add(CreateTag());
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var caseState = await PendingTagFakes.ActivateAsync(PendingTagFakes.CreateRepository(openCase), openCase);
        int changes = 0;
        var announced = new List<AttachResult>();
        tags.OnChange += () => changes++;
        tags.Attached += announced.Add;

        // Act
        AttachResult result = await tags.AttachToActiveCaseAsync(caseState, CancellationToken.None);

        // Assert
        Assert.Equal(1, changes);
        AttachResult announcedResult = Assert.Single(announced);
        Assert.Equal(result, announcedResult);
        Assert.Same(announcedResult, tags.LastAttach);
        Assert.NotNull(tags.LastAttach?.AtUtc);
    }

    [Fact]
    public async Task PendingEvidenceTags_LastAttach_IsNullUntilSomethingAttaches()
    {
        // Arrange
        PendingEvidenceTags tags = CreateTags();
        _ = tags.Add(CreateTag());
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var unactivated = PendingTagFakes.CreateUnactivated(PendingTagFakes.CreateRepository(openCase));

        // Act and Assert
        _ = await tags.AttachToActiveCaseAsync(unactivated, CancellationToken.None);
        Assert.Null(tags.LastAttach);
    }
}
