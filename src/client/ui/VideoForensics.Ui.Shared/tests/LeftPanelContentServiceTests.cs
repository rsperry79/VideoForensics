namespace VideoForensics.Ui.Shared.Tests;

using Microsoft.AspNetCore.Components;
using Xunit;
using VideoForensics.Ui.Shared.Services;

public class LeftPanelContentServiceTests
{
    [Fact]
    public void Set_WithContent_RaisesOnChangeAndExposesContent()
    {
        // Arrange
        var service = new LeftPanelContentService();
        RenderFragment content = _ => { };
        var raised = 0;
        service.OnChange += () => raised++;

        // Act
        service.Set(content);

        // Assert
        Assert.Same(content, service.Content);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Clear_WhenContentSet_ClearsAndRaisesOnChange()
    {
        // Arrange
        var service = new LeftPanelContentService();
        service.Set(_ => { });
        var raised = 0;
        service.OnChange += () => raised++;

        // Act
        service.Clear();

        // Assert
        Assert.Null(service.Content);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Clear_WhenNoContent_DoesNotRaiseOnChange()
    {
        // Arrange
        var service = new LeftPanelContentService();
        var raised = 0;
        service.OnChange += () => raised++;

        // Act
        service.Clear();

        // Assert
        Assert.Null(service.Content);
        Assert.Equal(0, raised);
    }
}
