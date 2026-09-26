using Microsoft.JSInterop;

using Moq;
using Xunit;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Ui.Shared.Tests;

public class UiModeServiceTests
{
    [Fact]
    public void UiModeService_Constructor_DefaultMode_IsStandard()
    {
        // Arrange & Act
        var mockRepository = Mock.Of<IOperatorPreferencesRepository>();
        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>()).Object;
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
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns((Guid?)null);

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
        mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferences?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns(operatorId);

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
        var savedPreferences = new OperatorPreferences
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            ThemeMode = "Dark",
            CultureName = "en-US",
            UiMode = "Simple"
        };

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns(operatorId);

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
        var savedPreferences = new OperatorPreferences
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            ThemeMode = "Light",
            CultureName = "en-US",
            UiMode = "Standard"
        };

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns(operatorId);

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
        mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferences?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns(operatorId);

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
        mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferences?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act
        await service.SetModeAsync("Simple");

        // Assert
        mockRepository.Verify(
            r => r.UpsertAsync(
                It.Is<OperatorPreferences>(p => p.OperatorId == operatorId && p.UiMode == "Simple"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_DoesNotClobberOtherFields()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var savedPreferences = new OperatorPreferences
        {
            Id = Guid.NewGuid(),
            OperatorId = operatorId,
            ThemeMode = "Dark",
            CultureName = "en-US",
            UiMode = "Standard"
        };

        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act
        await service.SetModeAsync("Simple");

        // Assert - verify that ThemeMode and CultureName are preserved
        mockRepository.Verify(
            r => r.UpsertAsync(
                It.Is<OperatorPreferences>(p =>
                    p.OperatorId == operatorId &&
                    p.UiMode == "Simple" &&
                    p.ThemeMode == "Dark" &&
                    p.CultureName == "en-US"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UiModeService_SetModeAsync_FiresOnChangeEvent()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferences?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns(operatorId);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        var changeCount = 0;
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
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns((Guid?)null);

        var service = new UiModeService(mockRepository.Object, mockSession.Object);
        await service.InitializeAsync();

        // Act
        await service.SetModeAsync("Simple");

        // Assert - repository should not be called when no operator is signed in
        mockRepository.Verify(r => r.UpsertAsync(It.IsAny<OperatorPreferences>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UiModeService_InitializeAsync_CanBeCalledMultipleTimes_OnlyInitializesOnce()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var mockRepository = new Mock<IOperatorPreferencesRepository>();
        mockRepository
            .Setup(r => r.GetAsync(operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorPreferences?)null);

        var mockSession = new Mock<PairedSessionState>(Mock.Of<IJSRuntime>());
        mockSession.Setup(s => s.EnsureLoadedAsync()).Returns(Task.CompletedTask);
        mockSession.Setup(s => s.OperatorId).Returns(operatorId);

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
}
