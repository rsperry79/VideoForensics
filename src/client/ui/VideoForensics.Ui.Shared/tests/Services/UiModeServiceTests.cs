using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class UiModeServiceTests
{
    /// <summary>Builds a preferences DTO for the tests, with the fields the layout service reads set explicitly.</summary>
    private static OperatorPreferencesDto Prefs(
        Guid operatorId,
        string uiMode,
        bool uiModeLocked = false,
        string themeMode = "System",
        string? cultureName = null,
        string timeFormat = "Locale")
    {
        return new OperatorPreferencesDto(
            Guid.NewGuid(),
            operatorId,
            themeMode,
            cultureName,
            uiMode,
            uiModeLocked,
            timeFormat,
            false,
            DateTime.UtcNow);
    }

    [Fact]
    public void UiModeService_Constructor_DefaultMode_IsStandard()
    {
        // Arrange & Act
        IOperatorPreferencesRepository mockRepository = Mock.Of<IOperatorPreferencesRepository>();
        PairedSessionState mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>()).Object;
        var service = new UiModeService(mockRepository, mockSession);

        // Assert
        Assert.Equal("Standard", service.Mode);
    }

    [Fact]
    public async Task UiModeService_InitializeAsync_NoSignedInOperator_ModeRemains()
    {
        // Arrange
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());

        // Simulate no signed-in operator
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns((Guid?)null);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.Equal("Standard", service.Mode);
    }

    [Fact]
    public async Task UiModeService_InitializeAsync_SignedInOperatorNoSavedPreferences_DefaultsToStandard()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferencesDto?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.Equal("Standard", service.Mode);
    }

    [Fact]
    public async Task UiModeService_InitializeAsync_SignedInOperatorWithSimpleMode_LoadsSimpleMode()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var savedPreferences = Prefs(operatorId, uiMode: "Simple", themeMode: "Dark", cultureName: "en-US");

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.Equal("Simple", service.Mode);
    }

    [Fact]
    public async Task UiModeService_InitializeAsync_SignedInOperatorWithStandardMode_LoadsStandardMode()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var savedPreferences = Prefs(operatorId, uiMode: "Standard", themeMode: "Light", cultureName: "en-US");

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.Equal("Standard", service.Mode);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_UpdatesLocalMode()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferencesDto?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act
        await service.SetModeAsync("Simple");

        // Assert
        Assert.Equal("Simple", service.Mode);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_SignedInOperator_PersiststoRepository()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferencesDto?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act
        await service.SetModeAsync("Simple");

        // Assert
        mockRepository.Verify(
            r => r.UpsertAsync(
                It.Is<OperatorPreferencesDto>(p => p.OperatorId == operatorId && p.UiMode == "Simple"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_DoesNotClobberOtherFields()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var savedPreferences = Prefs(operatorId, uiMode: "Standard", themeMode: "Dark", cultureName: "en-US", timeFormat: "TwentyFourHour");

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act
        await service.SetModeAsync("Simple");

        // Assert - verify that ThemeMode, CultureName and TimeFormat are preserved
        mockRepository.Verify(
            r => r.UpsertAsync(
                It.Is<OperatorPreferencesDto>(p =>
                    p.OperatorId == operatorId &&
                    p.UiMode == "Simple" &&
                    p.ThemeMode == "Dark" &&
                    p.CultureName == "en-US" &&
                    p.TimeFormat == "TwentyFourHour"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_FiresOnChangeEvent()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferencesDto?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        int changeCount = 0;
        service.OnChange += () => changeCount++;

        // Act
        await service.SetModeAsync("Simple");

        // Assert
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_NoSignedInOperator_DoesNotPersist()
    {
        // Arrange
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns((Guid?)null);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act
        await service.SetModeAsync("Simple");

        // Assert - repository should not be called when no operator is signed in
        mockRepository.Verify(r => r.UpsertAsync(It.IsAny<OperatorPreferencesDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UiModeService_InitializeAsync_CanBeCalledMultipleTimes_OnlyInitializesOnce()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferencesDto?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);

        // Act
        await service.InitializeAsync();
        await service.InitializeAsync();
        await service.InitializeAsync();

        // Assert - repository should only be queried once
        mockRepository.Verify(
            r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UiModeService_InitializeAsync_LoadsIsLockedFalse()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var savedPreferences = Prefs(operatorId, uiMode: "Simple", uiModeLocked: false);

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.False(service.IsLocked);
    }

    [Fact]
    public async Task UiModeService_InitializeAsync_LoadsIsLockedTrue()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var savedPreferences = Prefs(operatorId, uiMode: "Standard", uiModeLocked: true);

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);

        // Act
        await service.InitializeAsync();

        // Assert
        Assert.True(service.IsLocked);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_WhenLocked_ThrowsInvalidOperationException()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var savedPreferences = Prefs(operatorId, uiMode: "Standard", uiModeLocked: true);

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetModeAsync("Simple"));
        Assert.Equal("The display mode is locked by an administrator.", ex.Message);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_WhenLocked_DoesNotChangeMode()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var savedPreferences = Prefs(operatorId, uiMode: "Standard", uiModeLocked: true);

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act
        try
        {
            await service.SetModeAsync("Simple");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        Assert.Equal("Standard", service.Mode);
        mockRepository.Verify(
            r => r.UpsertAsync(It.IsAny<OperatorPreferencesDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_InvalidMode_ThrowsArgumentException()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferencesDto?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentException>(() => service.SetModeAsync("InvalidMode"));
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_RepositoryThrowsLockException_RefreshesFromRepository()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var initialPrefs = Prefs(operatorId, uiMode: "Standard", uiModeLocked: false);
        var refreshedPrefs = initialPrefs with { UiModeLocked = true };

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        _ = mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialPrefs);

        // First UpsertAsync call (from SetModeAsync) throws lock exception
        _ = mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<OperatorPreferencesDto>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // After the exception, subsequent GetAsync calls return the locked version
                _ = mockRepository
                    .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(refreshedPrefs);
            })
            .ThrowsAsync(new InvalidOperationException("The display mode is locked by an administrator."));

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        _ = mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        _ = mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        int changeCount = 0;
        service.OnChange += () => changeCount++;

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetModeAsync("Simple"));
        Assert.Equal("The display mode is locked by an administrator.", ex.Message);

        // Verify that Mode and IsLocked were refreshed from the repository
        Assert.True(service.IsLocked);
        Assert.Equal(1, changeCount);
    }
}
