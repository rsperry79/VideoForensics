using Moq;

using System.Reflection;

using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.Ui.Shared.Tests.Review;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Cases;

public class CaseState_Tests
{
    private Mock<ICaseRepository> CreateMockCaseRepository()
    {
        return new Mock<ICaseRepository>();
    }

    private ScopeState CreateScopeState()
    {
        var now = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        return new ScopeState(new FakeTimeProvider(now));
    }

    private ForensicCaseDto CreateTestCase(
        Guid? id = null,
        string caseNumber = "CASE-001",
        string title = "Test Case",
        CaseStatusDto status = CaseStatusDto.Open,
        DateTime? scopeFromUtc = null,
        DateTime? scopeToUtc = null,
        IReadOnlyList<Guid>? deviceIds = null)
    {
        return new ForensicCaseDto(
            Id: id ?? Guid.NewGuid(),
            CaseNumber: caseNumber,
            Title: title,
            Description: null,
            Status: status.ToString(),
            LeadOperatorId: null,
            CreatedBy: "test",
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow,
            ClosedBy: null,
            ClosedAtUtc: null,
            ScopeFromUtc: scopeFromUtc,
            ScopeToUtc: scopeToUtc,
            DeviceIds: deviceIds ?? []);
    }

    [Fact]
    public async Task ActivateAsync_WithValidId_LoadsCaseAndAppliesScope()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        ForensicCaseDto testCase = CreateTestCase(
            id: caseId,
            scopeFromUtc: new DateTime(2026, 9, 1),
            scopeToUtc: new DateTime(2026, 9, 10),
            deviceIds: new[] { device1Id, device2Id });

        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        _ = mockRepo
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        _ = mockRepo
            .Setup(r => r.GetDeviceIdsAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device1Id, device2Id }.ToList());

        ScopeState scopeState = CreateScopeState();
        var caseState = new CaseState(mockRepo.Object, scopeState);

        // Act
        bool result = await caseState.ActivateAsync(caseId, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.NotNull(caseState.ActiveCase);
        Assert.Equal(caseId, caseState.ActiveCase.Id);
        Assert.Equal("CASE-001", caseState.ActiveCase.CaseNumber);
        Assert.Contains(device1Id, scopeState.Current.DeviceIds);
        Assert.Contains(device2Id, scopeState.Current.DeviceIds);
        Assert.Equal(new DateTime(2026, 9, 1), scopeState.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 10), scopeState.Current.ToUtc);
    }

    [Fact]
    public async Task ActivateAsync_WithUnknownId_ReturnsFalseAndDoesNotChangeState()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        _ = mockRepo
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ForensicCaseDto?)null);

        ScopeState scopeState = CreateScopeState();
        var caseState = new CaseState(mockRepo.Object, scopeState);
        ForensicScope originalScope = scopeState.Current;

        // Act
        bool result = await caseState.ActivateAsync(caseId, CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.Null(caseState.ActiveCase);
        Assert.Equal(originalScope, scopeState.Current);
    }

    [Fact]
    public async Task ActivateAsync_WithoutScopeInCase_KeepsCurrentWindowButAppliesDevices()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var device1Id = Guid.NewGuid();
        ForensicCaseDto testCase = CreateTestCase(
            id: caseId,
            scopeFromUtc: null,
            scopeToUtc: null);

        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        _ = mockRepo
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        _ = mockRepo
            .Setup(r => r.GetDeviceIdsAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device1Id }.ToList());

        ScopeState scopeState = CreateScopeState();
        DateTime originalFromUtc = scopeState.Current.FromUtc;
        DateTime originalToUtc = scopeState.Current.ToUtc;
        var caseState = new CaseState(mockRepo.Object, scopeState);

        // Act
        _ = await caseState.ActivateAsync(caseId, CancellationToken.None);

        // Assert - devices are applied, window stays
        Assert.Contains(device1Id, scopeState.Current.DeviceIds);
        Assert.Equal(originalFromUtc, scopeState.Current.FromUtc);
        Assert.Equal(originalToUtc, scopeState.Current.ToUtc);
    }

    [Fact]
    public void Deactivate_ClearsActiveCase()
    {
        // Arrange
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        ScopeState scopeState = CreateScopeState();
        var caseState = new CaseState(mockRepo.Object, scopeState);
        ForensicCaseDto testCase = CreateTestCase();

        caseState._setActiveCaseForTest(testCase);
        Assert.NotNull(caseState.ActiveCase);

        // Act
        caseState.Deactivate();

        // Assert
        Assert.Null(caseState.ActiveCase);
    }

    [Fact]
    public void ScopeDiffersFromCase_WithNullWindow_ReturnsTrue()
    {
        // Arrange - case with no saved scope (both null)
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        ScopeState scopeState = CreateScopeState();
        ForensicCaseDto testCase = CreateTestCase(scopeFromUtc: null, scopeToUtc: null);

        var caseState = new CaseState(mockRepo.Object, scopeState);
        caseState._setActiveCaseForTest(testCase);

        // Act
        bool differs = caseState.ScopeDiffersFromCase;

        // Assert
        Assert.True(differs);
    }

    [Fact]
    public void ScopeDiffersFromCase_WhenDevicesAndWindowMatch_ReturnsFalse()
    {
        // Arrange
        _ = Guid.NewGuid();
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        ScopeState scopeState = CreateScopeState();
        ForensicCaseDto testCase = CreateTestCase(scopeFromUtc: scopeState.Current.FromUtc, scopeToUtc: scopeState.Current.ToUtc);

        var caseState = new CaseState(mockRepo.Object, scopeState);
        caseState._setActiveCaseForTest(testCase);

        // Set scope to match the case exactly
        scopeState.Set(scopeState.Current with
        {
            DeviceIds = [],
            FromUtc = testCase.ScopeFromUtc ?? scopeState.Current.FromUtc,
            ToUtc = testCase.ScopeToUtc ?? scopeState.Current.ToUtc
        });

        // Act
        bool differs = caseState.ScopeDiffersFromCase;

        // Assert
        Assert.False(differs);
    }

    [Fact]
    public void ScopeDiffersFromCase_WhenDevicesDiffer_ReturnsTrue()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        ScopeState scopeState = CreateScopeState();
        ForensicCaseDto testCase = CreateTestCase(scopeFromUtc: scopeState.Current.FromUtc, scopeToUtc: scopeState.Current.ToUtc);

        var caseState = new CaseState(mockRepo.Object, scopeState);
        caseState._setActiveCaseForTest(testCase);

        // Change devices
        scopeState.Set(scopeState.Current with { DeviceIds = [device1Id, device2Id] });

        // Act
        bool differs = caseState.ScopeDiffersFromCase;

        // Assert
        Assert.True(differs);
    }

    [Fact]
    public void ScopeDiffersFromCase_WhenWindowDiffers_ReturnsTrue()
    {
        // Arrange
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        ScopeState scopeState = CreateScopeState();
        ForensicCaseDto testCase = CreateTestCase(scopeFromUtc: new DateTime(2026, 9, 1), scopeToUtc: new DateTime(2026, 9, 10));

        var caseState = new CaseState(mockRepo.Object, scopeState);
        caseState._setActiveCaseForTest(testCase);

        // Current scope will be default from ScopeState, which differs from the case
        // Act
        bool differs = caseState.ScopeDiffersFromCase;

        // Assert
        Assert.True(differs);
    }

    [Fact]
    public async Task SaveScopeToCaseAsync_CallsRepositoryWithExactArguments()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        _ = mockRepo
            .Setup(r => r.SetScopeAsync(
                caseId,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = mockRepo
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestCase(id: caseId));
        _ = mockRepo
            .Setup(r => r.GetDeviceIdsAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device1Id, device2Id }.ToList());

        ScopeState scopeState = CreateScopeState();
        scopeState.Set(scopeState.Current with { DeviceIds = [device1Id, device2Id] });

        var caseState = new CaseState(mockRepo.Object, scopeState);
        ForensicCaseDto testCase = CreateTestCase(id: caseId);
        caseState._setActiveCaseForTest(testCase);

        string actor = Guid.NewGuid().ToString();

        // Act
        await caseState.SaveScopeToCaseAsync(actor, CancellationToken.None);

        // Assert
        mockRepo.Verify(r => r.SetScopeAsync(
            caseId,
            scopeState.Current.FromUtc,
            scopeState.Current.ToUtc,
            It.Is<IReadOnlyCollection<Guid>>(devices => devices.SequenceEqual(new[] { device1Id, device2Id })),
            actor,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PinAsync_WithOpenCase_CallsRepositoryAddItemAsync()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        var caseItem = new CaseItemDto(
            Id: Guid.NewGuid(),
            CaseId: caseId,
            Kind: CaseItemKindDto.Event.ToString(),
            TargetId: eventId,
            Reason: "Test reason",
            AddedBy: "test-actor",
            AddedAtUtc: DateTime.UtcNow,
            MediaSha256AtAdd: null,
            RemovedBy: null,
            RemovedAtUtc: null,
            RemovalReason: null);
        _ = mockRepo
            .Setup(r => r.AddItemAsync(
                caseId,
                CaseItemKindDto.Event,
                eventId,
                "Test reason",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseItem);

        ScopeState scopeState = CreateScopeState();
        var caseState = new CaseState(mockRepo.Object, scopeState);
        ForensicCaseDto testCase = CreateTestCase(id: caseId, status: CaseStatusDto.Open);
        caseState._setActiveCaseForTest(testCase);

        // Act
        CaseItemDto result = await caseState.PinAsync(CaseItemKindDto.Event, eventId, "Test reason", "test-actor", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.TargetId);
        mockRepo.Verify(r => r.AddItemAsync(
            caseId,
            CaseItemKindDto.Event,
            eventId,
            "Test reason",
            "test-actor",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HasActivePinAsync_WithPinnedSnapshot_ReturnsTrue()
    {
        var caseId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        _ = mockRepo
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new CaseItemDto(
                    Id: Guid.NewGuid(),
                    CaseId: caseId,
                    Kind: CaseItemKindDto.Snapshot.ToString(),
                    TargetId: snapshotId,
                    Reason: "r",
                    AddedBy: "a",
                    AddedAtUtc: DateTime.UtcNow,
                    MediaSha256AtAdd: null,
                    RemovedBy: null,
                    RemovedAtUtc: null,
                    RemovalReason: null),
            ]);
        var caseState = new CaseState(mockRepo.Object, CreateScopeState());
        caseState._setActiveCaseForTest(CreateTestCase(id: caseId, status: CaseStatusDto.Open));

        bool pinned = await caseState.HasActivePinAsync(CaseItemKindDto.Snapshot, snapshotId, CancellationToken.None);
        bool otherPinned = await caseState.HasActivePinAsync(CaseItemKindDto.Snapshot, Guid.NewGuid(), CancellationToken.None);

        Assert.True(pinned);
        Assert.False(otherPinned);
    }

    [Fact]
    public async Task PinAsync_WithoutActiveCase_ThrowsInvalidOperationException()
    {
        // Arrange
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        ScopeState scopeState = CreateScopeState();
        var caseState = new CaseState(mockRepo.Object, scopeState);
        // No case activated

        // Act & Assert
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => caseState.PinAsync(CaseItemKindDto.Event, Guid.NewGuid(), "reason", "actor", CancellationToken.None));
    }

    [Fact]
    public async Task PinAsync_WithClosedCase_ThrowsInvalidOperationException()
    {
        // Arrange
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        ScopeState scopeState = CreateScopeState();
        var caseState = new CaseState(mockRepo.Object, scopeState);
        ForensicCaseDto testCase = CreateTestCase(status: CaseStatusDto.Closed);
        caseState._setActiveCaseForTest(testCase);

        // Act & Assert
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => caseState.PinAsync(CaseItemKindDto.Event, Guid.NewGuid(), "reason", "actor", CancellationToken.None));
    }

    [Fact]
    public async Task CaseState_ActivateAsync_WithPendingTags_AttachesThemOnceAndClearsPending()
    {
        // Arrange
        var pending = new PendingEvidenceTags(Microsoft.Extensions.Logging.Abstractions.NullLogger<PendingEvidenceTags>.Instance);
        _ = pending.Add(new PendingTag(CaseItemKindDto.Media, Guid.NewGuid(), Guid.NewGuid(), PendingTagFakes.NowUtc, "Tagged during review", "actor-1"));
        _ = pending.Add(new PendingTag(CaseItemKindDto.Event, Guid.NewGuid(), Guid.NewGuid(), PendingTagFakes.NowUtc, "Tagged during review", "actor-1"));
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        Mock<ICaseRepository> repository = PendingTagFakes.CreateRepository(openCase);

        // Act
        CaseState caseState = await PendingTagFakes.ActivateAsync(repository, openCase, pending);
        _ = await caseState.ActivateAsync(openCase.Id, CancellationToken.None);

        // Assert
        repository.Verify(r => r.AddItemAsync(
            openCase.Id, It.IsAny<CaseItemKindDto>(), It.IsAny<Guid>(), It.IsAny<string>(), "actor-1", It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal(0, pending.Count);
    }

    [Fact]
    public async Task CaseState_ActivateAsync_WhenAttachThrows_StillReturnsTrueAndActivatesTheCase()
    {
        // Arrange
        var pending = new ThrowingPendingEvidenceTags();
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        Mock<ICaseRepository> repository = PendingTagFakes.CreateRepository(openCase);
        var caseState = new CaseState(repository.Object, new ScopeState(new FakeTimeProvider(PendingTagFakes.NowUtc)), pending);

        // Act
        bool result = await caseState.ActivateAsync(openCase.Id, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.NotNull(caseState.ActiveCase);
    }

    [Fact]
    public async Task CaseState_ActivateAsync_WithoutPendingService_DoesNotPinOrReadItems()
    {
        // Arrange
        ForensicCaseDto openCase = PendingTagFakes.CreateOpenCase();
        Mock<ICaseRepository> repository = PendingTagFakes.CreateRepository(openCase);
        var caseState = new CaseState(repository.Object, new ScopeState(new FakeTimeProvider(PendingTagFakes.NowUtc)));

        // Act
        bool result = await caseState.ActivateAsync(openCase.Id, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.NotNull(caseState.ActiveCase);
        repository.Verify(r => r.AddItemAsync(
            It.IsAny<Guid>(), It.IsAny<CaseItemKindDto>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.ListItemsAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>Pending tags whose attach always throws, so a test can prove activation survives a failed attach.</summary>
internal sealed class ThrowingPendingEvidenceTags : PendingEvidenceTags
{
    public ThrowingPendingEvidenceTags()
        : base(Microsoft.Extensions.Logging.Abstractions.NullLogger<PendingEvidenceTags>.Instance)
    {
    }

    public override Task<AttachResult> AttachToActiveCaseAsync(CaseState caseState, CancellationToken ct)
    {
        throw new InvalidOperationException("Attach failed in the test double.");
    }
}

// Internal extension methods for testing
internal static class CaseStateTestExtensions
{
    internal static void _setActiveCaseForTest(this CaseState state, ForensicCaseDto caseItem)
    {
        PropertyInfo? property = typeof(CaseState).GetProperty("ActiveCase");
        property?.SetValue(state, caseItem);
    }
}
