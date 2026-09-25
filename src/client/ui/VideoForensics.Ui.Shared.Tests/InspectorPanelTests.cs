namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;
using VideoForensics.Ui.Shared.Components.Inspector;
using VideoForensics.Ui.Shared.Services.Inspector;

public class InspectorPanel_Rendering_Tests : BunitContext
{
    public InspectorPanel_Rendering_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_Nothing_WhenStateCurrentIsNull()
    {
        // Arrange
        var state = new InspectorState();
        Services.AddScoped(_ => state);

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        Assert.Empty(component.Markup.Trim());
    }

    [Fact]
    public void Renders_Title_WhenStateHasModel()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(
            Title: "Test Device",
            Fields: new { Id = 1, Name = "Device1" });
        state.Show(model);
        Services.AddScoped(_ => state);

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        var markup = component.Markup;
        Assert.Contains("Test Device", markup);
    }

    [Fact]
    public void Renders_FieldsTab_AsDefault()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(
            Title: "TestTitle",
            Fields: new { TestField = "TestValue" });
        state.Show(model);
        Services.AddScoped(_ => state);

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        var activeTab = component.Find("[data-testid='inspector-tab-fields'].active");
        Assert.NotNull(activeTab);
    }

    [Fact]
    public void Renders_CloseButton_WithCorrectTestId()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(
            Title: "TestTitle",
            Fields: new { });
        state.Show(model);
        Services.AddScoped(_ => state);

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        var closeButton = component.Find("[data-testid='inspector-close']");
        Assert.NotNull(closeButton);
    }

    [Fact]
    public void CloseButton_ClearsState()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(
            Title: "TestTitle",
            Fields: new { });
        state.Show(model);
        Services.AddScoped(_ => state);

        var component = Render<InspectorPanel>();

        // Act
        var closeButton = component.Find("[data-testid='inspector-close']");
        closeButton.Click();

        // Assert
        Assert.Null(state.Current);
        Assert.Empty(component.Markup.Trim());
    }

    [Fact]
    public void ClickingRawTab_ShowsRawJson()
    {
        // Arrange
        var state = new InspectorState();
        var rawJson = "{\"metadata\":\"value\"}";
        var model = new InspectorModel(
            Title: "TestTitle",
            Fields: new { },
            RawJson: rawJson);
        state.Show(model);
        Services.AddScoped(_ => state);

        var component = Render<InspectorPanel>();

        // Act
        var rawTab = component.Find("[data-testid='inspector-tab-raw']");
        rawTab.Click();

        // Assert
        var markup = component.Markup;
        Assert.Contains("metadata", markup);
        Assert.Contains("value", markup);
    }

    [Fact]
    public void RawTab_ShowsMessageWhenNoRawJson()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(
            Title: "TestTitle",
            Fields: new { },
            RawJson: null);
        state.Show(model);
        Services.AddScoped(_ => state);

        var component = Render<InspectorPanel>();

        // Act
        var rawTab = component.Find("[data-testid='inspector-tab-raw']");
        rawTab.Click();

        // Assert
        var markup = component.Markup;
        Assert.Contains("No raw provider data", markup);
    }

    [Fact]
    public void RelatedTab_RendersLinks()
    {
        // Arrange
        var state = new InspectorState();
        var related = new[]
        {
            new InspectorLink(Text: "Link1", Href: "/path1"),
            new InspectorLink(Text: "Link2", Href: "/path2")
        };
        var model = new InspectorModel(
            Title: "TestTitle",
            Fields: new { },
            Related: related);
        state.Show(model);
        Services.AddScoped(_ => state);

        var component = Render<InspectorPanel>();

        // Act
        var relatedTab = component.Find("[data-testid='inspector-tab-related']");
        relatedTab.Click();

        // Assert
        var links = component.FindAll("a");
        Assert.Contains(links, a => a.TextContent == "Link1" && a.GetAttribute("href") == "/path1");
        Assert.Contains(links, a => a.TextContent == "Link2" && a.GetAttribute("href") == "/path2");
    }

    [Fact]
    public void ProvenanceTab_RendersKeyValueTable()
    {
        // Arrange
        var state = new InspectorState();
        var provenance = new[]
        {
            new KeyValuePair<string, string>("Source", "API"),
            new KeyValuePair<string, string>("Timestamp", "2025-01-01")
        };
        var model = new InspectorModel(
            Title: "TestTitle",
            Fields: new { },
            Provenance: provenance);
        state.Show(model);
        Services.AddScoped(_ => state);

        var component = Render<InspectorPanel>();

        // Act
        var provenanceTab = component.Find("[data-testid='inspector-tab-provenance']");
        provenanceTab.Click();

        // Assert
        var markup = component.Markup;
        Assert.Contains("Source", markup);
        Assert.Contains("API", markup);
        Assert.Contains("Timestamp", markup);
        Assert.Contains("2025-01-01", markup);
    }

    [Fact]
    public void Show_NewModel_ResetsToFieldsTabAndShowsNewData()
    {
        // Arrange
        var state = new InspectorState();
        var model1 = new InspectorModel(
            Title: "Title1",
            Fields: new { Value = "First" },
            RawJson: "{\"raw\":\"data1\"}");
        state.Show(model1);
        Services.AddScoped(_ => state);

        var component = Render<InspectorPanel>();

        // Verify raw tab can be clicked
        var rawTab = component.Find("[data-testid='inspector-tab-raw']");
        rawTab.Click();
        Assert.NotNull(component.Find("[data-testid='inspector-tab-raw'].active"));

        // Act - show a new model (need to do this in component context)
        var model2 = new InspectorModel(
            Title: "Title2",
            Fields: new { Value = "Second" });

        component.InvokeAsync(() =>
        {
            state.Show(model2);
        });

        // Assert - fields tab should be active again
        var fieldsTab = component.Find("[data-testid='inspector-tab-fields'].active");
        Assert.NotNull(fieldsTab);

        // Verify new model's title is displayed
        Assert.Contains("Title2", component.Markup);
    }

}
