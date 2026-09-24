namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;
using Syncfusion.Blazor;
using VideoForensics.Ui.Shared.Components.Inspector;
using VideoForensics.Ui.Shared.Services.Inspector;

public class ForensicGrid_ToolbarItem_Tests
{
    [Theory]
    [InlineData("grid_csvexport", true)]
    [InlineData("grid_excelexport", false)]
    [InlineData("grid_columnchooser", false)]
    [InlineData("csvexport", true)]
    [InlineData(null, false)]
    public void IsCsvExportItem_IdentifiesCsvExportToolbar(string? itemId, bool expected)
    {
        var result = ForensicGrid<object>.IsCsvExportItem(itemId);
        Assert.Equal(expected, result);
    }
}

public class ForensicGrid_CsvExportProperties_Tests
{
    [Fact]
    public void BuildCsvExportProperties_WithNullFileName_DefaultsToExportCsv()
    {
        var properties = ForensicGrid<object>.BuildCsvExportProperties(null);

        Assert.Equal("export.csv", properties.FileName);
    }

    [Fact]
    public void BuildCsvExportProperties_WithWhitespaceFileName_DefaultsToExportCsv()
    {
        var properties = ForensicGrid<object>.BuildCsvExportProperties("   ");

        Assert.Equal("export.csv", properties.FileName);
    }

    [Fact]
    public void BuildCsvExportProperties_WithCustomFileName_UsesProvidedFileName()
    {
        var properties = ForensicGrid<object>.BuildCsvExportProperties("evidence-log.csv");

        Assert.Equal("evidence-log.csv", properties.FileName);
    }
}

public class ForensicGrid_Tests : BunitContext
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// A real, minimal subclass of <see cref="Syncfusion.Blazor.Grids.SfGrid{TValue}"/> used as a
    /// bUnit component double for <c>SfGrid&lt;TestItem&gt;</c>. bUnit's generic
    /// <c>ComponentFactories.AddStub</c> substitutes an unrelated wrapper type, which is not
    /// assignable to <c>SfGrid&lt;TestItem&gt;</c> and therefore breaks <see cref="ForensicGrid{TItem}"/>'s
    /// typed <c>@ref</c> capture (an <see cref="InvalidCastException"/> at render time, since Blazor's
    /// generated reference-capture code always casts to the concrete component type). Because this
    /// double genuinely inherits from <see cref="Syncfusion.Blazor.Grids.SfGrid{TValue}"/>, that cast
    /// succeeds while still avoiding the real grid's rendering/JS-interop behavior.
    /// </summary>
    private sealed class TestSfGrid : Syncfusion.Blazor.Grids.SfGrid<TestItem>
    {
        // Skip the real grid's lifecycle/JS-interop behavior entirely; this double only
        // needs to exist and be assignable to SfGrid<TestItem> for @ref capture to succeed.
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
        }

        protected override Task OnInitializedAsync() => Task.CompletedTask;

        protected override Task OnParametersSetAsync() => Task.CompletedTask;

        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }

    public ForensicGrid_Tests()
    {
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        ComponentFactories.Add<Syncfusion.Blazor.Grids.SfGrid<TestItem>, TestSfGrid>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridColumn>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridPageSettings>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridEvents<TestItem>>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task HandleRowSelectedAsync_WithToInspector_SetsInspectorState()
    {
        // Arrange
        var state = new InspectorState();
        var item = new TestItem { Id = 1, Name = "Test Item", CreatedAt = DateTime.UtcNow };
        InspectorModel? mappedModel = null;

        InspectorModel MapToInspector(TestItem row) =>
            new(
                Title: row.Name,
                Fields: new { row.Id, row.Name });

        Services.AddScoped(_ => state);

        var component = Render<ForensicGrid<TestItem>>(
            parameters => parameters
                .Add(p => p.DataSource, new[] { item })
                .Add(p => p.ToInspector, MapToInspector));

        // Act
        await component.InvokeAsync(async () =>
        {
            await component.Instance.HandleRowSelectedAsync(item);
            mappedModel = state.Current;
        });

        // Assert
        Assert.NotNull(mappedModel);
        Assert.Equal("Test Item", mappedModel.Title);
        Assert.NotNull(mappedModel.Fields);
    }

    [Fact]
    public async Task HandleRowSelectedAsync_WithoutToInspector_DoesNotSetInspectorState()
    {
        // Arrange
        var state = new InspectorState();
        var item = new TestItem { Id = 1, Name = "Test Item", CreatedAt = DateTime.UtcNow };

        Services.AddScoped(_ => state);

        var component = Render<ForensicGrid<TestItem>>(
            parameters => parameters
                .Add(p => p.DataSource, new[] { item })
                .Add(p => p.ToInspector, null));

        // Act
        await component.InvokeAsync(async () =>
        {
            await component.Instance.HandleRowSelectedAsync(item);
        });

        // Assert
        Assert.Null(state.Current);
    }

    [Fact]
    public async Task HandleRowSelectedAsync_InvokesOnRowSelected()
    {
        // Arrange
        var state = new InspectorState();
        var item = new TestItem { Id = 1, Name = "Test Item", CreatedAt = DateTime.UtcNow };
        var callbackInvoked = false;
        TestItem? callbackItem = null;

        Services.AddScoped(_ => state);

        var component = Render<ForensicGrid<TestItem>>(
            parameters => parameters
                .Add(p => p.DataSource, new[] { item })
                .Add(p => p.OnRowSelected, EventCallback.Factory.Create<TestItem>(
                    this,
                    i =>
                    {
                        callbackInvoked = true;
                        callbackItem = i;
                    })));

        // Act
        await component.InvokeAsync(async () =>
        {
            await component.Instance.HandleRowSelectedAsync(item);
        });

        // Assert
        Assert.True(callbackInvoked);
        Assert.Equal(item, callbackItem);
    }

    [Fact]
    public async Task HandleRowSelectedAsync_WithToInspector_AndOnRowSelected_ExecutesBoth()
    {
        // Arrange
        var state = new InspectorState();
        var item = new TestItem { Id = 1, Name = "Test Item", CreatedAt = DateTime.UtcNow };
        var callbackInvoked = false;

        InspectorModel MapToInspector(TestItem row) =>
            new(
                Title: row.Name,
                Fields: new { row.Id, row.Name });

        Services.AddScoped(_ => state);

        var component = Render<ForensicGrid<TestItem>>(
            parameters => parameters
                .Add(p => p.DataSource, new[] { item })
                .Add(p => p.ToInspector, MapToInspector)
                .Add(p => p.OnRowSelected, EventCallback.Factory.Create<TestItem>(
                    this,
                    _ => { callbackInvoked = true; })));

        // Act
        await component.InvokeAsync(async () =>
        {
            await component.Instance.HandleRowSelectedAsync(item);
        });

        // Assert
        Assert.NotNull(state.Current);
        Assert.Equal("Test Item", state.Current.Title);
        Assert.True(callbackInvoked);
    }

}
