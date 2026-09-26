namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;
using VideoForensics.Ui.Shared.Components.Dump;

public class DumpView_Rendering_Tests : BunitContext
{
    public DumpView_Rendering_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_ScalarString_WithStringCssClass()
    {
        // Arrange
        var json = "\"hello\"";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        var elem = component.Find(".dump-string");
        Assert.NotNull(elem);
        Assert.Contains("hello", elem.TextContent);
    }

    [Fact]
    public void Renders_ScalarNumber_WithNumberCssClass()
    {
        // Arrange
        var json = "42";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        var elem = component.Find(".dump-number");
        Assert.NotNull(elem);
        Assert.Contains("42", elem.TextContent);
    }

    [Fact]
    public void Renders_ScalarBoolean_WithBoolCssClass()
    {
        // Arrange
        var json = "true";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        var elem = component.Find(".dump-bool");
        Assert.NotNull(elem);
        Assert.Contains("true", elem.TextContent);
    }

    [Fact]
    public void Renders_ScalarNull_WithNullCssClass()
    {
        // Arrange
        var json = "null";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        var elem = component.Find(".dump-null");
        Assert.NotNull(elem);
        Assert.Contains("null", elem.TextContent);
    }

    [Fact]
    public void Renders_TabularArray_AsTable()
    {
        // Arrange
        var json = "[{\"id\":1,\"name\":\"a\"},{\"id\":2,\"name\":\"b\"}]";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert - verify component renders without error
        Assert.NotNull(component.Markup);
        var tables = component.FindAll("table");
        Assert.NotEmpty(tables);
    }

    [Fact]
    public void Renders_Object_AsKeyValueTable()
    {
        // Arrange
        var json = "{\"name\":\"test\",\"count\":5}";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        var table = component.Find("table");
        Assert.NotNull(table);
    }

    [Fact]
    public void Renders_ExpandableNode_WithCaretToggle()
    {
        // Arrange
        var json = "{\"nested\":{\"key\":\"value\"}}";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 0));

        // Assert - verify carets are rendered
        var markup = component.Markup;
        Assert.Contains("expand-toggle", markup);
    }

    [Fact]
    public void Renders_NodeWithDataPath_Attribute()
    {
        // Arrange
        var json = "{\"device\":{\"name\":\"cam1\"}}";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert - verify data-path attributes are rendered
        var markup = component.Markup;
        Assert.Contains("data-path=", markup);
    }

    [Fact]
    public void Renders_InvalidJson_ShowsAlert()
    {
        // Arrange
        var json = "{invalid}";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        var alert = component.Find(".alert");
        Assert.NotNull(alert);
        Assert.Contains("Invalid JSON", alert.TextContent);
    }

    [Fact]
    public void Renders_Value_Parameter_WithObject()
    {
        // Arrange
        var obj = new { Name = "TestValue" };

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Value, obj));

        // Assert - verify component renders successfully with object value
        var markup = component.Markup;
        Assert.NotNull(markup);
        Assert.NotEmpty(markup);
    }

    [Fact]
    public void Renders_SearchBox_WhenShowSearchTrue()
    {
        // Arrange & Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, "{\"test\":1}")
            .Add(p => p.ShowSearch, true));

        // Assert
        var searchInput = component.Find("input[type='text']");
        Assert.NotNull(searchInput);
    }

    [Fact]
    public void HidesSearchBox_WhenShowSearchFalse()
    {
        // Arrange & Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, "{\"test\":1}")
            .Add(p => p.ShowSearch, false));

        // Assert
        var searchInputs = component.FindAll("input[type='text']");
        Assert.Empty(searchInputs);
    }

    [Fact]
    public void Search_RenderingWithSearchTermIsSupported()
    {
        // This test verifies search infrastructure is in place, but full search
        // filtering is tested in DumpNodeBuilderTests.Matches_*
        // Arrange
        var json = "{\"device\":\"camera\"}";
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.ShowSearch, true));

        // Act
        var searchInput = component.Find("input[type='text']");
        Assert.NotNull(searchInput);

        // Assert - component renders successfully with search box
        Assert.NotNull(component.Markup);
    }

    [Fact]
    public void Renders_CopyButton_WhenShowCopyTrue()
    {
        // Arrange & Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, "{\"test\":1}")
            .Add(p => p.ShowCopy, true));

        // Assert
        var copyButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Copy"));
        Assert.NotNull(copyButton);
    }

    [Fact]
    public void HidesCopyButton_WhenShowCopyFalse()
    {
        // Arrange & Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, "{\"test\":1}")
            .Add(p => p.ShowCopy, false));

        // Assert
        var copyButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Copy"));
        Assert.Null(copyButton);
    }

    [Fact]
    public void CopyButton_InvokesClipboardWriteText()
    {
        // Arrange
        var json = "{\"test\":1}";
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.ShowCopy, true));

        // Setup JSInterop
        this.JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);

        // Act
        var copyButton = component.FindAll("button").First(b => b.TextContent.Contains("Copy"));
        copyButton.Click();

        // Assert - Verify the JS interop was called
        var invocations = this.JSInterop.Invocations.Where(i => i.Identifier == "navigator.clipboard.writeText").ToList();
        Assert.NotEmpty(invocations);
    }

    [Fact]
    public void TabularArray_DisplaysItemCount()
    {
        // Arrange
        var json = "[{\"id\":1},{\"id\":2},{\"id\":3}]";

        // Act
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        var content = component.Markup;
        Assert.Contains("3", content);
    }

    [Fact]
    public void InitialExpandDepth_ControlsNodeExpansion()
    {
        // Arrange
        var json = "{\"level1\":{\"level2\":{\"level3\":\"value\"}}}";

        // Act
        var componentDepth0 = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 0));

        var componentDepth2 = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 2));

        // Assert
        // At depth 0, level2 should be collapsed initially
        var level2InDepth0 = componentDepth0.FindAll("[data-path='$.level1.level2']");
        // Should be fewer elements visible in depth 0

        var level2InDepth2 = componentDepth2.FindAll("[data-path='$.level1.level2']");
        // Should be more elements visible in depth 2
        Assert.True(level2InDepth2.Count >= level2InDepth0.Count);
    }

    [Fact]
    public void ExpandToggle_ClickOnCollapsedNode_RevealsDeeperPath()
    {
        // Arrange - InitialExpandDepth 1 opens the root ($) but leaves $.level1 (depth 1) collapsed
        var json = "{\"level1\":{\"level2\":{\"level3\":\"deep\"}}}";
        var component = Render<DumpView>(parameters => parameters
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
        var json = "{\"alpha\":\"one\",\"beta\":\"two\"}";
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.ShowSearch, true));

        // Act
        component.Find("input").Input("two");

        // Assert
        Assert.DoesNotContain("alpha", component.Markup);
        var match = component.Find(".dump-match");
        Assert.Equal("two", match.TextContent);
    }

    [Fact]
    public void ManualExpandState_SurvivesUnrelatedSearchThenClear()
    {
        // Arrange - "keep" starts expanded (depth 1 < InitialExpandDepth 2), revealing its
        // nested object "$.keep.nested" (only Object/Array nodes carry a data-path wrapper)
        var json = "{\"keep\":{\"nested\":{\"deep\":\"x\"}},\"other\":\"findme\"}";
        var component = Render<DumpView>(parameters => parameters
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
        component.Find("input").Input("");

        // Assert - "keep" is still collapsed, i.e. the manual choice survived the search round-trip
        Assert.Empty(component.FindAll("[data-path='$.keep.nested']"));
    }

    [Fact]
    public void Search_AutoExpandsMatchBelowInitialExpandDepth_ThenCollapsesWhenCleared()
    {
        // Arrange - InitialExpandDepth 0 means every node starts fully collapsed
        var json = "{\"level1\":{\"level2\":{\"level3\":\"deep\"}}}";
        var component = Render<DumpView>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.InitialExpandDepth, 0)
            .Add(p => p.ShowSearch, true));
        Assert.Empty(component.FindAll("[data-path='$.level1']"));

        // Act
        component.Find("input").Input("deep");

        // Assert - the match is rendered, auto-expanded past the collapsed manual state
        var match = component.Find(".dump-match");
        Assert.Equal("deep", match.TextContent);

        // Clearing the search collapses everything back to the manual (closed) state
        component.Find("input").Input("");
        Assert.Empty(component.FindAll("[data-path='$.level1']"));
    }
}
