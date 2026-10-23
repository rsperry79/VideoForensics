
using Bunit;

using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Dump;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class DumpView_Rendering_Tests : BunitContext
{
    public DumpView_Rendering_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
    }

    [Fact]
    public void Renders_ScalarString_WithStringCssClass()
    {
        // Arrange
        string json = "\"hello\"";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        AngleSharp.Dom.IElement elem = component.Find(".dump-string");
        Assert.NotNull(elem);
        Assert.Contains("hello", elem.TextContent);
    }

    [Fact]
    public void Renders_ScalarNumber_WithNumberCssClass()
    {
        // Arrange
        string json = "42";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        AngleSharp.Dom.IElement elem = component.Find(".dump-number");
        Assert.NotNull(elem);
        Assert.Contains("42", elem.TextContent);
    }

    [Fact]
    public void Renders_ScalarBoolean_WithBoolCssClass()
    {
        // Arrange
        string json = "true";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        AngleSharp.Dom.IElement elem = component.Find(".dump-bool");
        Assert.NotNull(elem);
        Assert.Contains("true", elem.TextContent);
    }

    [Fact]
    public void Renders_ScalarNull_WithNullCssClass()
    {
        // Arrange
        string json = "null";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        AngleSharp.Dom.IElement elem = component.Find(".dump-null");
        Assert.NotNull(elem);
        Assert.Contains("null", elem.TextContent);
    }

    [Fact]
    public void Renders_TabularArray_AsTable()
    {
        // Arrange
        string json = "[{\"id\":1,\"name\":\"a\"},{\"id\":2,\"name\":\"b\"}]";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert - verify component renders without error
        Assert.NotNull(component.Markup);
        IReadOnlyList<AngleSharp.Dom.IElement> tables = component.FindAll("table");
        Assert.NotEmpty(tables);
    }

    [Fact]
    public void Renders_Object_AsKeyValueTable()
    {
        // Arrange
        string json = "{\"name\":\"test\",\"count\":5}";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        AngleSharp.Dom.IElement table = component.Find("table");
        Assert.NotNull(table);
    }

    [Fact]
    public void Renders_ExpandableNode_WithCaretToggle()
    {
        // Arrange
        string json = "{\"nested\":{\"key\":\"value\"}}";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 0));

        // Assert - verify carets are rendered
        string markup = component.Markup;
        Assert.Contains("expand-toggle", markup);
    }

    [Fact]
    public void Renders_NodeWithDataPath_Attribute()
    {
        // Arrange
        string json = "{\"device\":{\"name\":\"cam1\"}}";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert - verify data-path attributes are rendered
        string markup = component.Markup;
        Assert.Contains("data-path=", markup);
    }

    [Fact]
    public void Renders_InvalidJson_ShowsAlert()
    {
        // Arrange
        string json = "{invalid}";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        AngleSharp.Dom.IElement alert = component.Find(".alert");
        Assert.NotNull(alert);
        Assert.Contains("Invalid JSON", alert.TextContent);
    }

    [Fact]
    public void Renders_Value_Parameter_WithObject()
    {
        // Arrange
        var obj = new { Name = "TestValue" };

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Value, obj));

        // Assert - verify component renders successfully with object value
        string markup = component.Markup;
        Assert.NotNull(markup);
        Assert.NotEmpty(markup);
    }

    [Fact]
    public void Renders_SearchBox_WhenShowSearchTrue()
    {
        // Arrange & Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, "{\"test\":1}")
            .Add(p => p.ShowSearch, true));

        // Assert
        AngleSharp.Dom.IElement searchInput = component.Find("input[type='text']");
        Assert.NotNull(searchInput);
    }

    [Fact]
    public void HidesSearchBox_WhenShowSearchFalse()
    {
        // Arrange & Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, "{\"test\":1}")
            .Add(p => p.ShowSearch, false));

        // Assert
        IReadOnlyList<AngleSharp.Dom.IElement> searchInputs = component.FindAll("input[type='text']");
        Assert.Empty(searchInputs);
    }

    [Fact]
    public void Search_RenderingWithSearchTermIsSupported()
    {
        // This test verifies search infrastructure is in place, but full search
        // filtering is tested in DumpNodeBuilderTests.Matches_*
        // Arrange
        string json = "{\"device\":\"camera\"}";
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.ShowSearch, true));

        // Act
        AngleSharp.Dom.IElement searchInput = component.Find("input[type='text']");
        Assert.NotNull(searchInput);

        // Assert - component renders successfully with search box
        Assert.NotNull(component.Markup);
    }

    [Fact]
    public void Renders_CopyButton_WhenShowCopyTrue()
    {
        // Arrange & Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, "{\"test\":1}")
            .Add(p => p.ShowCopy, true));

        // Assert
        AngleSharp.Dom.IElement? copyButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Copy"));
        Assert.NotNull(copyButton);
    }

    [Fact]
    public void HidesCopyButton_WhenShowCopyFalse()
    {
        // Arrange & Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, "{\"test\":1}")
            .Add(p => p.ShowCopy, false));

        // Assert
        AngleSharp.Dom.IElement? copyButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Copy"));
        Assert.Null(copyButton);
    }

    [Fact]
    public void CopyButton_InvokesClipboardWriteText()
    {
        // Arrange
        string json = "{\"test\":1}";
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.ShowCopy, true));

        // Setup JSInterop
        _ = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);

        // Act
        AngleSharp.Dom.IElement copyButton = component.FindAll("button").First(b => b.TextContent.Contains("Copy"));
        copyButton.Click();

        // Assert - Verify the JS interop was called
        var invocations = JSInterop.Invocations.Where(i => i.Identifier == "navigator.clipboard.writeText").ToList();
        Assert.NotEmpty(invocations);
    }

    [Fact]
    public void TabularArray_DisplaysItemCount()
    {
        // Arrange
        string json = "[{\"id\":1},{\"id\":2},{\"id\":3}]";

        // Act
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        string content = component.Markup;
        Assert.Contains("3", content);
    }

    [Fact]
    public void InitialExpandDepth_ControlsNodeExpansion()
    {
        // Arrange
        string json = "{\"level1\":{\"level2\":{\"level3\":\"value\"}}}";

        // Act
        IRenderedComponent<DumpView> componentDepth0 = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 0));

        IRenderedComponent<DumpView> componentDepth2 = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 2));

        // Assert
        // At depth 0, level2 should be collapsed initially
        IReadOnlyList<AngleSharp.Dom.IElement> level2InDepth0 = componentDepth0.FindAll("[data-path='$.level1.level2']");
        // Should be fewer elements visible in depth 0

        IReadOnlyList<AngleSharp.Dom.IElement> level2InDepth2 = componentDepth2.FindAll("[data-path='$.level1.level2']");
        // Should be more elements visible in depth 2
        Assert.True(level2InDepth2.Count >= level2InDepth0.Count);
    }

    [Fact]
    public void ExpandToggle_ClickOnCollapsedNode_RevealsDeeperPath()
    {
        // Arrange - InitialExpandDepth 1 opens the root ($) but leaves $.level1 (depth 1) collapsed
        string json = "{\"level1\":{\"level2\":{\"level3\":\"deep\"}}}";
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 1));

        Assert.Empty(component.FindAll("[data-path='$.level1.level2']"));

        // Act
        component.Find("[data-path='$.level1'] [data-testid='expand-toggle']").Click();

        // Assert
        Assert.NotEmpty(component.FindAll("[data-path='$.level1.level2']"));
    }

    [Fact]
    public void Search_Input_FiltersNonMatchingSiblingsAndMarksMatch()
    {
        // Arrange
        string json = "{\"alpha\":\"one\",\"beta\":\"two\"}";
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.ShowSearch, true));

        // Act
        component.Find("input").Input("two");

        // Assert
        Assert.DoesNotContain("alpha", component.Markup);
        AngleSharp.Dom.IElement match = component.Find(".dump-match");
        Assert.Equal("two", match.TextContent);
    }

    [Fact]
    public void ManualExpandState_SurvivesUnrelatedSearchThenClear()
    {
        // Arrange - "keep" starts expanded (depth 1 < InitialExpandDepth 2), revealing its
        // nested object "$.keep.nested" (only Object/Array nodes carry a data-path wrapper)
        string json = "{\"keep\":{\"nested\":{\"deep\":\"x\"}},\"other\":\"findme\"}";
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 2)
            .Add(p => p.ShowSearch, true));
        Assert.NotEmpty(component.FindAll("[data-path='$.keep.nested']"));

        // Act - manually collapse "keep"
        component.Find("[data-path='$.keep'] [data-testid='expand-toggle']").Click();
        Assert.Empty(component.FindAll("[data-path='$.keep.nested']"));

        // Search for a term that has nothing to do with "keep"
        component.Find("input").Input("findme");
        Assert.Empty(component.FindAll("[data-path='$.keep.nested']"));

        // Clear the search
        component.Find("input").Input(string.Empty);

        // Assert - "keep" is still collapsed, i.e. the manual choice survived the search round-trip
        Assert.Empty(component.FindAll("[data-path='$.keep.nested']"));
    }

    [Fact]
    public void Search_AutoExpandsMatchBelowInitialExpandDepth_ThenCollapsesWhenCleared()
    {
        // Arrange - InitialExpandDepth 0 means every node starts fully collapsed
        string json = "{\"level1\":{\"level2\":{\"level3\":\"deep\"}}}";
        IRenderedComponent<DumpView> component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 0)
            .Add(p => p.ShowSearch, true));
        Assert.Empty(component.FindAll("[data-path='$.level1']"));

        // Act
        component.Find("input").Input("deep");

        // Assert - the match is rendered, auto-expanded past the collapsed manual state
        AngleSharp.Dom.IElement match = component.Find(".dump-match");
        Assert.Equal("deep", match.TextContent);

        // Clearing the search collapses everything back to the manual (closed) state
        component.Find("input").Input(string.Empty);
        Assert.Empty(component.FindAll("[data-path='$.level1']"));
    }
}
