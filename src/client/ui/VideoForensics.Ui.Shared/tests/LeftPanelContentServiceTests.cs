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

    [Fact]
    public void Clear_ByAnEarlierOwner_KeepsTheContentAnotherPageSetSince()
    {
        // Blazor sets the next page's content before disposing the previous page, so the previous
        // page's Clear must not wipe content that now belongs to the next page.
        var service = new LeftPanelContentService();
        var previousPage = new object();
        var nextPage = new object();
        RenderFragment next = _ => { };
        service.Set(_ => { }, previousPage);
        service.Set(next, nextPage);

        service.Clear(previousPage);

        Assert.Same(next, service.Content);
    }

    [Fact]
    public void Clear_ByCurrentOwner_ClearsItsContent()
    {
        var service = new LeftPanelContentService();
        var page = new object();
        service.Set(_ => { }, page);

        service.Clear(page);

        Assert.Null(service.Content);
    }
}
