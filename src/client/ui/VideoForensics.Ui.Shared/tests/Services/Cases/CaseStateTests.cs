using Moq;

using System.Reflection;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.Ui.Shared.Tests;

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

    private ForensicCase CreateTestCase(
        Guid? id = null,
        string caseNumber = "CASE-001",
        string title = "Test Case",
        CaseStatus status = CaseStatus.Open,
        DateTime? scopeFromUtc = null,
        DateTime? scopeToUtc = null,
        IReadOnlyList<Guid>? deviceIds = null)
    {
        return new ForensicCase
        {
            Id = id ?? Guid.NewGuid(),
            CaseNumber = caseNumber,
            Title = title,
            Status = status,
            ScopeFromUtc = scopeFromUtc,
            ScopeToUtc = scopeToUtc,
            CreatedBy = "test",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task ActivateAsync_WithValidId_LoadsCaseAndAppliesScope()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        ForensicCase testCase = CreateTestCase(
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
            .ReturnsAsync((ForensicCase?)null);

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
        ForensicCase testCase = CreateTestCase(
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
        ForensicCase testCase = CreateTestCase();

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
        ForensicCase testCase = CreateTestCase(scopeFromUtc: null, scopeToUtc: null);

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
        ForensicCase testCase = CreateTestCase(scopeFromUtc: scopeState.Current.FromUtc, scopeToUtc: scopeState.Current.ToUtc);

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
        ForensicCase testCase = CreateTestCase(scopeFromUtc: scopeState.Current.FromUtc, scopeToUtc: scopeState.Current.ToUtc);

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
        ForensicCase testCase = CreateTestCase(scopeFromUtc: new DateTime(2026, 9, 1), scopeToUtc: new DateTime(2026, 9, 10));

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
        ForensicCase testCase = CreateTestCase(id: caseId);
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
        var caseItem = new CaseItem
        {
            Id = Guid.NewGuid(),
            Kind = CaseItemKind.Event,
            EventId = eventId,
            Reason = "Test reason",
            AddedBy = "test-actor"
        };
        _ = mockRepo
            .Setup(r => r.AddItemAsync(
                caseId,
                CaseItemKind.Event,
                eventId,
                "Test reason",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseItem);

        ScopeState scopeState = CreateScopeState();
        var caseState = new CaseState(mockRepo.Object, scopeState);
        ForensicCase testCase = CreateTestCase(id: caseId, status: CaseStatus.Open);
        caseState._setActiveCaseForTest(testCase);

        // Act
        CaseItem result = await caseState.PinAsync(CaseItemKind.Event, eventId, "Test reason", "test-actor", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        mockRepo.Verify(r => r.AddItemAsync(
            caseId,
            CaseItemKind.Event,
            eventId,
            "Test reason",
            "test-actor",
            It.IsAny<CancellationToken>()), Times.Once);
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
            () => caseState.PinAsync(CaseItemKind.Event, Guid.NewGuid(), "reason", "actor", CancellationToken.None));
    }

    [Fact]
    public async Task PinAsync_WithClosedCase_ThrowsInvalidOperationException()
    {
        // Arrange
        Mock<ICaseRepository> mockRepo = CreateMockCaseRepository();
        ScopeState scopeState = CreateScopeState();
        var caseState = new CaseState(mockRepo.Object, scopeState);
        ForensicCase testCase = CreateTestCase(status: CaseStatus.Closed);
        caseState._setActiveCaseForTest(testCase);

        // Act & Assert
        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => caseState.PinAsync(CaseItemKind.Event, Guid.NewGuid(), "reason", "actor", CancellationToken.None));
    }
}

// Internal extension methods for testing
internal static class CaseStateTestExtensions
{
    internal static void _setActiveCaseForTest(this CaseState state, ForensicCase caseItem)
    {
        PropertyInfo? property = typeof(CaseState).GetProperty("ActiveCase");
        property?.SetValue(state, caseItem);
    }
}
