using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Ui.Shared.Tests;

public class DefaultViewportServiceTests
{
    [Fact]
    public void DefaultViewportService_DetermineIsMobile_BelowBreakpoint_ReturnsTrue()
    {
        // Arrange
        double phoneWidth = 375;

        // Act
        bool result = DefaultViewportService.DetermineIsMobile(phoneWidth);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DefaultViewportService_DetermineIsMobile_AtBreakpoint_ReturnsFalse()
    {
        // Arrange
        double breakpointWidth = 600;

        // Act
        bool result = DefaultViewportService.DetermineIsMobile(breakpointWidth);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DefaultViewportService_DetermineIsMobile_AboveBreakpoint_ReturnsFalse()
    {
        // Arrange
        double desktopWidth = 1024;

        // Act
        bool result = DefaultViewportService.DetermineIsMobile(desktopWidth);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DefaultViewportService_OnViewportChanged_ValueChanges_FiresOnChange()
    {
        // Arrange
        var mockJs = Mock.Of<IJSRuntime>();
        var service = new DefaultViewportService(mockJs);
        var changeCount = 0;

        service.OnChange += () => changeCount++;

        // Act
        service.OnViewportChanged(true);

        // Assert
        Assert.Equal(1, changeCount);
        Assert.True(service.IsMobile);
    }

    [Fact]
    public void DefaultViewportService_OnViewportChanged_SameValue_DoesNotFireOnChange()
    {
        // Arrange
        var mockJs = Mock.Of<IJSRuntime>();
        var service = new DefaultViewportService(mockJs);
        var changeCount = 0;

        service.OnChange += () => changeCount++;

        // Default IsMobile is false
        Assert.False(service.IsMobile);

        // Act
        service.OnViewportChanged(false);

        // Assert - should not fire OnChange since value didn't actually change
        Assert.Equal(0, changeCount);
        Assert.False(service.IsMobile);
    }

    [Fact]
    public void DefaultViewportService_OnViewportChanged_MultipleChanges_FiresOnChangeEachTime()
    {
        // Arrange
        var mockJs = Mock.Of<IJSRuntime>();
        var service = new DefaultViewportService(mockJs);
        var changeCount = 0;

        service.OnChange += () => changeCount++;

        // Act - change from false to true
        service.OnViewportChanged(true);

        // Assert
        Assert.Equal(1, changeCount);
        Assert.True(service.IsMobile);

        // Act - change from true to false
        service.OnViewportChanged(false);

        // Assert
        Assert.Equal(2, changeCount);
        Assert.False(service.IsMobile);
    }
}
