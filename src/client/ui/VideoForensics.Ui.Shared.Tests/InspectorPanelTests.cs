namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components.Inspector;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Cases;
using VideoForensics.Ui.Shared.Services.Inspector;
using VideoForensics.Ui.Shared.Services.Scope;

public class InspectorPanel_Rendering_Tests : BunitContext
{
    public InspectorPanel_Rendering_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Defaults: no active case, no signed-in operator. Pin tests override these with
        // RegisterCaseState / RegisterRoleAsync (later registrations win).
        RegisterCaseState(activeCase: null);
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    private static ForensicCase MakeCase(
        string caseNumber = "CASE-001",
        string title = "Test Case",
        CaseStatus status = CaseStatus.Open) => new()
    {
        Id = Guid.NewGuid(),
        CaseNumber = caseNumber,
        Title = title,
        Status = status,
        CreatedBy = "test",
        CreatedAtUtc = DateTime.UtcNow
    };

    private (CaseState CaseState, Mock<ICaseRepository> Repository) RegisterCaseState(
        ForensicCase? activeCase,
        Mock<ICaseRepository>? repository = null)
    {
        repository ??= new Mock<ICaseRepository>();
        var caseState = new CaseState(repository.Object, new ScopeState());
        if (activeCase is not null)
        {
            caseState._setActiveCaseForTest(activeCase);
        }

        Services.AddScoped(_ => caseState);
        return (caseState, repository);
    }

    private async Task RegisterRoleAsync(OperatorRole role)
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), role.ToString());
        Services.AddScoped(_ => session);
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

    [Fact]
    public void NoActiveCase_WithPin_ShowsHint_NoButton()
    {
        // Arrange
        var state = new InspectorState();
        state.Show(new InspectorModel(Title: "TestTitle", Pin: new PinTarget(CaseItemKind.Event, Guid.NewGuid())));
        Services.AddScoped(_ => state);

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        Assert.Contains("Select a case in the left panel to pin evidence.", component.Markup);
        Assert.Empty(component.FindAll("[data-testid='pin-to-case']"));
    }

    [Fact]
    public async Task OpenCase_ReviewRole_ShowsPinButton()
    {
        // Arrange
        var state = new InspectorState();
        state.Show(new InspectorModel(Title: "TestTitle", Pin: new PinTarget(CaseItemKind.Event, Guid.NewGuid())));
        Services.AddScoped(_ => state);
        RegisterCaseState(MakeCase());
        await RegisterRoleAsync(OperatorRole.Review);

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        Assert.NotEmpty(component.FindAll("[data-testid='pin-to-case']"));
    }

    [Fact]
    public async Task PinButton_DisabledWhenReasonBlank()
    {
        // Arrange
        var state = new InspectorState();
        state.Show(new InspectorModel(Title: "TestTitle", Pin: new PinTarget(CaseItemKind.Event, Guid.NewGuid())));
        Services.AddScoped(_ => state);
        RegisterCaseState(MakeCase());
        await RegisterRoleAsync(OperatorRole.Review);

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        var button = (AngleSharp.Html.Dom.IHtmlButtonElement)component.Find("[data-testid='pin-to-case']");
        Assert.True(button.IsDisabled);
    }

    [Fact]
    public async Task PinButton_Click_CallsAddItemAsync_WithReasonAndTarget()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var state = new InspectorState();
        state.Show(new InspectorModel(Title: "TestTitle", Pin: new PinTarget(CaseItemKind.Event, eventId)));
        Services.AddScoped(_ => state);

        var testCase = MakeCase();
        var repository = new Mock<ICaseRepository>();
        repository
            .Setup(r => r.AddItemAsync(testCase.Id, CaseItemKind.Event, eventId, "Confirmed match", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaseItem { Id = Guid.NewGuid(), Kind = CaseItemKind.Event, EventId = eventId, Reason = "Confirmed match", AddedBy = "test" });
        RegisterCaseState(testCase, repository);
        await RegisterRoleAsync(OperatorRole.Review);

        var component = Render<InspectorPanel>();
        var reasonBox = component.Find("[data-testid='pin-reason']");
        reasonBox.Input("Confirmed match");

        // Act
        var button = component.Find("[data-testid='pin-to-case']");
        button.Click();

        // Assert
        repository.Verify(r => r.AddItemAsync(testCase.Id, CaseItemKind.Event, eventId, "Confirmed match", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains($"Pinned to {testCase.CaseNumber}", component.Markup);
    }

    [Fact]
    public async Task PinButton_Click_RepositoryThrowsInvalidOperationException_ShowsMessage()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var state = new InspectorState();
        state.Show(new InspectorModel(Title: "TestTitle", Pin: new PinTarget(CaseItemKind.Event, eventId)));
        Services.AddScoped(_ => state);

        var testCase = MakeCase();
        var repository = new Mock<ICaseRepository>();
        repository
            .Setup(r => r.AddItemAsync(testCase.Id, CaseItemKind.Event, eventId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Item is already pinned to this case."));
        RegisterCaseState(testCase, repository);
        await RegisterRoleAsync(OperatorRole.Review);

        var component = Render<InspectorPanel>();
        component.Find("[data-testid='pin-reason']").Input("Duplicate reason");

        // Act
        component.Find("[data-testid='pin-to-case']").Click();

        // Assert
        Assert.Contains("Item is already pinned to this case.", component.Markup);
    }

    [Fact]
    public void ClosedCase_NoPinButton()
    {
        // Arrange
        var state = new InspectorState();
        state.Show(new InspectorModel(Title: "TestTitle", Pin: new PinTarget(CaseItemKind.Event, Guid.NewGuid())));
        Services.AddScoped(_ => state);
        RegisterCaseState(MakeCase(status: CaseStatus.Closed));

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        Assert.Empty(component.FindAll("[data-testid='pin-to-case']"));
    }

    [Fact]
    public async Task ReadOnlyRole_NoPinButton()
    {
        // Arrange
        var state = new InspectorState();
        state.Show(new InspectorModel(Title: "TestTitle", Pin: new PinTarget(CaseItemKind.Event, Guid.NewGuid())));
        Services.AddScoped(_ => state);
        RegisterCaseState(MakeCase());
        await RegisterRoleAsync(OperatorRole.ReadOnly);

        // Act
        var component = Render<InspectorPanel>();

        // Assert
        Assert.Empty(component.FindAll("[data-testid='pin-to-case']"));
    }

    [Fact]
    public async Task NewInspectedItem_ClearsPreviousSuccessMessage()
    {
        // Arrange
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();
        var state = new InspectorState();
        state.Show(new InspectorModel(Title: "Title1", Pin: new PinTarget(CaseItemKind.Event, eventId1)));
        Services.AddScoped(_ => state);

        var testCase = MakeCase();
        var repository = new Mock<ICaseRepository>();
        repository
            .Setup(r => r.AddItemAsync(testCase.Id, CaseItemKind.Event, eventId1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaseItem { Id = Guid.NewGuid(), Kind = CaseItemKind.Event, EventId = eventId1, Reason = "r", AddedBy = "test" });
        RegisterCaseState(testCase, repository);
        await RegisterRoleAsync(OperatorRole.Review);

        var component = Render<InspectorPanel>();
        component.Find("[data-testid='pin-reason']").Input("Confirmed match");
        component.Find("[data-testid='pin-to-case']").Click();
        Assert.Contains($"Pinned to {testCase.CaseNumber}", component.Markup);

        // Act - a new item is shown in the inspector
        component.InvokeAsync(() =>
        {
            state.Show(new InspectorModel(Title: "Title2", Pin: new PinTarget(CaseItemKind.Event, eventId2)));
        });

        // Assert - success message and reason are cleared for the new item
        Assert.DoesNotContain($"Pinned to {testCase.CaseNumber}", component.Markup);
        var reasonBox = (AngleSharp.Html.Dom.IHtmlTextAreaElement)component.Find("[data-testid='pin-reason']");
        Assert.Equal(string.Empty, reasonBox.Value);
    }
}
