using Bunit;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The review page's tag action: pins straight to an open case, or keeps the tag pending until one opens.</summary>
public class ReviewEvidenceTagger_Tests : BunitContext
{
    private readonly Guid _operatorId = Guid.NewGuid();

    private async Task<PairedSessionState> CreateSessionAsync()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", _operatorId, "Review");
        return session;
    }

    private static ReviewEvidenceTagger CreateTagger(CaseState caseState, PendingEvidenceTags pending, PairedSessionState session)
    {
        return new ReviewEvidenceTagger(caseState, pending, session, NullLogger<ReviewEvidenceTagger>.Instance);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_NoActiveCase_ReturnsKeptPendingAndDoesNotPin()
    {
        // Arrange
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var repository = PendingTagFakes.CreateRepository(openCase);
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        var tagger = CreateTagger(PendingTagFakes.CreateUnactivated(repository), pending, await CreateSessionAsync());
        Guid targetId = Guid.NewGuid();

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKind.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.KeptPending, outcome);
        Assert.True(tagger.IsPending(CaseItemKind.Media, targetId));
        repository.Verify(r => r.AddItemAsync(
            It.IsAny<Guid>(), It.IsAny<CaseItemKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_ActiveCase_PinsWithKindTargetReasonAndActor()
    {
        // Arrange
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var repository = PendingTagFakes.CreateRepository(openCase);
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        var caseState = await PendingTagFakes.ActivateAsync(repository, openCase);
        var tagger = CreateTagger(caseState, pending, await CreateSessionAsync());
        Guid targetId = Guid.NewGuid();

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKind.Event, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.PinnedToOpenCase, outcome);
        Assert.Equal(0, pending.Count);
        repository.Verify(r => r.AddItemAsync(
            openCase.Id,
            CaseItemKind.Event,
            targetId,
            PendingTagFakes.Reason,
            _operatorId.ToString(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_DuplicatePending_ReturnsAlreadyTagged()
    {
        // Arrange
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        var tagger = CreateTagger(PendingTagFakes.CreateUnactivated(PendingTagFakes.CreateRepository(openCase)), pending, await CreateSessionAsync());
        Guid targetId = Guid.NewGuid();
        _ = await tagger.TagAsync(CaseItemKind.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKind.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.AlreadyTagged, outcome);
        Assert.Equal(1, pending.Count);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_ActiveCaseAlreadyHoldsItem_ReturnsAlreadyTagged()
    {
        // Arrange
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        Guid targetId = Guid.NewGuid();
        var existing = new CaseItem
        {
            Id = Guid.NewGuid(),
            CaseId = openCase.Id,
            Kind = CaseItemKind.Media,
            MediaItemId = targetId,
            Reason = "earlier",
            AddedBy = "actor-0",
        };
        var repository = PendingTagFakes.CreateRepository(openCase, existingItems: [existing]);
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        var caseState = await PendingTagFakes.ActivateAsync(repository, openCase);
        var tagger = CreateTagger(caseState, pending, await CreateSessionAsync());

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKind.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.AlreadyTagged, outcome);
        repository.Verify(r => r.AddItemAsync(
            It.IsAny<Guid>(), It.IsAny<CaseItemKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_PinRefused_ReturnsFailed()
    {
        // Arrange
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        Guid targetId = Guid.NewGuid();
        var repository = PendingTagFakes.CreateRepository(openCase, failingTargets: new HashSet<Guid> { targetId });
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        var caseState = await PendingTagFakes.ActivateAsync(repository, openCase);
        var tagger = CreateTagger(caseState, pending, await CreateSessionAsync());

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKind.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.Failed, outcome);
        Assert.Equal(0, pending.Count);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_IsPending_ReflectsTheKeptTag()
    {
        // Arrange
        ForensicCase openCase = PendingTagFakes.CreateOpenCase();
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        var tagger = CreateTagger(PendingTagFakes.CreateUnactivated(PendingTagFakes.CreateRepository(openCase)), pending, await CreateSessionAsync());
        Guid targetId = Guid.NewGuid();
        Assert.False(tagger.IsPending(CaseItemKind.Media, targetId));

        // Act
        _ = await tagger.TagAsync(CaseItemKind.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.True(tagger.IsPending(CaseItemKind.Media, targetId));
    }
}
