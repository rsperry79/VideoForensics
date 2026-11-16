using Bunit;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

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
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        Mock<ICaseRepository> repository = PendingTagFakes.CreateRepository(openCase);
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        ReviewEvidenceTagger tagger = CreateTagger(PendingTagFakes.CreateUnactivated(repository), pending, await CreateSessionAsync());
        var targetId = Guid.NewGuid();

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKindDto.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.KeptPending, outcome);
        Assert.True(tagger.IsPending(CaseItemKindDto.Media, targetId));
        repository.Verify(r => r.AddItemAsync(
            It.IsAny<Guid>(), It.IsAny<CaseItemKindDto>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_ActiveCase_PinsWithKindTargetReasonAndActor()
    {
        // Arrange
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        Mock<ICaseRepository> repository = PendingTagFakes.CreateRepository(openCase);
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        CaseState caseState = await PendingTagFakes.ActivateAsync(repository, openCase);
        ReviewEvidenceTagger tagger = CreateTagger(caseState, pending, await CreateSessionAsync());
        var targetId = Guid.NewGuid();

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKindDto.Event, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.PinnedToOpenCase, outcome);
        Assert.Equal(0, pending.Count);
        repository.Verify(r => r.AddItemAsync(
            openCase.Id,
            CaseItemKindDto.Event,
            targetId,
            PendingTagFakes.Reason,
            _operatorId.ToString(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_DuplicatePending_ReturnsAlreadyTagged()
    {
        // Arrange
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        ReviewEvidenceTagger tagger = CreateTagger(PendingTagFakes.CreateUnactivated(PendingTagFakes.CreateRepository(openCase)), pending, await CreateSessionAsync());
        var targetId = Guid.NewGuid();
        _ = await tagger.TagAsync(CaseItemKindDto.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKindDto.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.AlreadyTagged, outcome);
        Assert.Equal(1, pending.Count);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_ActiveCaseAlreadyHoldsItem_ReturnsAlreadyTagged()
    {
        // Arrange
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        var targetId = Guid.NewGuid();
        var existing = new CaseItemDto(Guid.NewGuid(), openCase.Id, CaseItemKindDto.Media.ToString(), targetId, "earlier", "actor-0", DateTime.UtcNow, null, null, null, null);
        Mock<ICaseRepository> repository = PendingTagFakes.CreateRepository(openCase, existingItems: [existing]);
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        CaseState caseState = await PendingTagFakes.ActivateAsync(repository, openCase);
        ReviewEvidenceTagger tagger = CreateTagger(caseState, pending, await CreateSessionAsync());

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKindDto.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.AlreadyTagged, outcome);
        repository.Verify(r => r.AddItemAsync(
            It.IsAny<Guid>(), It.IsAny<CaseItemKindDto>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_TagAsync_PinRefused_ReturnsFailed()
    {
        // Arrange
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        var targetId = Guid.NewGuid();
        Mock<ICaseRepository> repository = PendingTagFakes.CreateRepository(openCase, failingTargets: new HashSet<Guid> { targetId });
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        CaseState caseState = await PendingTagFakes.ActivateAsync(repository, openCase);
        ReviewEvidenceTagger tagger = CreateTagger(caseState, pending, await CreateSessionAsync());

        // Act
        TagOutcome outcome = await tagger.TagAsync(CaseItemKindDto.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.Equal(TagOutcome.Failed, outcome);
        Assert.Equal(0, pending.Count);
    }

    [Fact]
    public async Task ReviewEvidenceTagger_IsPending_ReflectsTheKeptTag()
    {
        // Arrange
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        var pending = new PendingEvidenceTags(NullLogger<PendingEvidenceTags>.Instance);
        ReviewEvidenceTagger tagger = CreateTagger(PendingTagFakes.CreateUnactivated(PendingTagFakes.CreateRepository(openCase)), pending, await CreateSessionAsync());
        var targetId = Guid.NewGuid();
        Assert.False(tagger.IsPending(CaseItemKindDto.Media, targetId));

        // Act
        _ = await tagger.TagAsync(CaseItemKindDto.Media, targetId, Guid.NewGuid(), null, PendingTagFakes.Reason, CancellationToken.None);

        // Assert
        Assert.True(tagger.IsPending(CaseItemKindDto.Media, targetId));
    }
}
