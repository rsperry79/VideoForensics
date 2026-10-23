
using Microsoft.AspNetCore.Components;

using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;

public class LeftPanelContentServiceTests
{
    [Fact]
    public void Set_WithContent_RaisesOnChangeAndExposesContent()
    {
        // Arrange
        var service = new LeftPanelContentService();
        RenderFragment content = _ => { };
        int raised = 0;
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
        int raised = 0;
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
        int raised = 0;
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
        object previousPage = new();
        object nextPage = new();
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
        object page = new();
        service.Set(_ => { }, page);

        service.Clear(page);

        Assert.Null(service.Content);
    }
}
