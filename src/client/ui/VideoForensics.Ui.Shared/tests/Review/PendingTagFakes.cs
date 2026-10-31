using Moq;

using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Utils.Testing;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Builders for the pending-tag tests: one open case behind a repository double, and a real <see cref="CaseState"/> over it.</summary>
internal static class PendingTagFakes
{
    /// <summary>The fixed clock the pending-tag tests run on.</summary>
    public static readonly DateTime NowUtc = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The reason text the review page stores on a pin; matches the resource value of TagDefaultReason.</summary>
    public const string Reason = "Tagged during review";

    /// <summary>Creates an open case with a fresh id.</summary>
    public static ForensicCase CreateOpenCase()
    {
        return new ForensicCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = "CASE-100",
            Title = "Open case",
            Status = CaseStatus.Open,
            CreatedBy = "test",
            CreatedAtUtc = NowUtc,
        };
    }

    /// <summary>
    /// Repository double for one case. Every pin is recorded in <paramref name="pinnedTargets"/> in call order.
    /// A target in <paramref name="failingTargets"/> throws on pin. <paramref name="existingItems"/> is what the case already holds.
    /// </summary>
    public static Mock<ICaseRepository> CreateRepository(
        ForensicCase activeCase,
        IReadOnlyList<CaseItem>? existingItems = null,
        ISet<Guid>? failingTargets = null,
        List<Guid>? pinnedTargets = null)
    {
        var repository = new Mock<ICaseRepository>();
        _ = repository
            .Setup(r => r.GetAsync(activeCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCase);
        _ = repository
            .Setup(r => r.GetDeviceIdsAsync(activeCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        _ = repository
            .Setup(r => r.ListItemsAsync(activeCase.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItems?.ToList() ?? new List<CaseItem>());
        _ = repository
            .Setup(r => r.AddItemAsync(
                activeCase.Id,
                It.IsAny<CaseItemKind>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid caseId, CaseItemKind kind, Guid targetId, string reason, string actor, CancellationToken _) =>
            {
                pinnedTargets?.Add(targetId);
                if (failingTargets is not null && failingTargets.Contains(targetId))
                {
                    return Task.FromException<CaseItem>(new InvalidOperationException("Pin refused by the test double."));
                }

                return Task.FromResult(new CaseItem
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    Kind = kind,
                    EventId = kind == CaseItemKind.Event ? targetId : null,
                    MediaItemId = kind == CaseItemKind.Media ? targetId : null,
                    Reason = reason,
                    AddedBy = actor,
                    AddedAtUtc = NowUtc,
                });
            });
        return repository;
    }

    /// <summary>Creates a case state over <paramref name="repository"/> and activates <paramref name="activeCase"/> on it.</summary>
    public static async Task<CaseState> ActivateAsync(
        Mock<ICaseRepository> repository,
        ForensicCase activeCase,
        PendingEvidenceTags? pendingTags = null)
    {
        var caseState = new CaseState(repository.Object, new ScopeState(new FakeTimeProvider(NowUtc)), pendingTags);
        _ = await caseState.ActivateAsync(activeCase.Id, CancellationToken.None);
        return caseState;
    }

    /// <summary>Creates a case state over <paramref name="repository"/> with no case activated.</summary>
    public static CaseState CreateUnactivated(Mock<ICaseRepository> repository)
    {
        return new CaseState(repository.Object, new ScopeState(new FakeTimeProvider(NowUtc)));
    }
}
