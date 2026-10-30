using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Syncfusion.Blazor.DropDowns;

using VideoForensics.Client.Ui.Shared.Components.QueryApi;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services.QueryApi;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The shared Query API filter bar. Devices and Events both render it; it owns the location and device
/// selections, which live in <see cref="IQueryApiFilterState"/> so they survive navigation between subpages.
/// </summary>
public class QueryApiFilterBar_Tests : BunitContext
{
    private static readonly QueryApiSelectOption[] Locations = [new("loc-1", "Home"), new("loc-2", "Office")];
    private static readonly QueryApiSelectOption[] Devices = [new("dev-1", "Lobby")];

    public QueryApiFilterBar_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped<IQueryApiFilterState, QueryApiFilterState>();
        _ = Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
    }

    [Fact]
    public void Render_WithDeviceElementId_RendersLocationAndDeviceDropdowns()
    {
        IRenderedComponent<QueryApiFilterBar> cut = Render<QueryApiFilterBar>(p => p
            .Add(c => c.LocationOptions, Locations)
            .Add(c => c.DeviceOptions, Devices)
            .Add(c => c.LocationElementId, "bar-location")
            .Add(c => c.DeviceElementId, "bar-device"));

        var dropdowns = cut.FindComponents<SfDropDownList<string, QueryApiSelectOption>>().Select(d => d.Instance.ID).ToList();
        Assert.Equal(new[] { "bar-location", "bar-device" }, dropdowns);
    }

    [Fact]
    public void Render_WithoutDeviceElementId_RendersOnlyLocationDropdown()
    {
        IRenderedComponent<QueryApiFilterBar> cut = Render<QueryApiFilterBar>(p => p
            .Add(c => c.LocationOptions, Locations)
            .Add(c => c.LocationElementId, "bar-location"));

        var dropdowns = cut.FindComponents<SfDropDownList<string, QueryApiSelectOption>>().Select(d => d.Instance.ID).ToList();
        Assert.Equal(new[] { "bar-location" }, dropdowns);
    }

    [Fact]
    public void Render_ShowsSelectionsHeldInFilterState()
    {
        IQueryApiFilterState state = Services.GetRequiredService<IQueryApiFilterState>();
        state.SetLocationId("loc-2");
        state.SetDeviceId("dev-1");

        IRenderedComponent<QueryApiFilterBar> cut = Render<QueryApiFilterBar>(p => p
            .Add(c => c.LocationOptions, Locations)
            .Add(c => c.DeviceOptions, Devices)
            .Add(c => c.LocationElementId, "bar-location")
            .Add(c => c.DeviceElementId, "bar-device"));

        var values = cut.FindComponents<SfDropDownList<string, QueryApiSelectOption>>().Select(d => d.Instance.Value).ToList();
        Assert.Equal(new[] { "loc-2", "dev-1" }, values);
    }

    [Fact]
    public async Task EmptyListReset_KeepsSelectionMadeElsewhere()
    {
        // The list has not loaded yet, so the dropdown clears its value. That must not wipe the shared selection.
        IQueryApiFilterState state = Services.GetRequiredService<IQueryApiFilterState>();
        state.SetLocationId("loc-2");
        IRenderedComponent<QueryApiFilterBar> cut = Render<QueryApiFilterBar>(p => p
            .Add(c => c.LocationOptions, Array.Empty<QueryApiSelectOption>())
            .Add(c => c.LocationElementId, "bar-location"));

        IRenderedComponent<SfDropDownList<string, QueryApiSelectOption>> dropdown = cut.FindComponent<SfDropDownList<string, QueryApiSelectOption>>();
        await cut.InvokeAsync(() => dropdown.Instance.ValueChanged.InvokeAsync(string.Empty));

        Assert.Equal("loc-2", state.LocationId);
    }

    [Fact]
    public async Task ClearingWhenOptionsExist_ClearsSelection()
    {
        IQueryApiFilterState state = Services.GetRequiredService<IQueryApiFilterState>();
        state.SetLocationId("loc-2");
        IRenderedComponent<QueryApiFilterBar> cut = Render<QueryApiFilterBar>(p => p
            .Add(c => c.LocationOptions, Locations)
            .Add(c => c.LocationElementId, "bar-location"));

        IRenderedComponent<SfDropDownList<string, QueryApiSelectOption>> dropdown = cut.FindComponent<SfDropDownList<string, QueryApiSelectOption>>();
        await cut.InvokeAsync(() => dropdown.Instance.ValueChanged.InvokeAsync(string.Empty));

        Assert.Null(state.LocationId);
    }

    [Fact]
    public async Task LocationChanged_WritesSelectionToFilterState()
    {
        IQueryApiFilterState state = Services.GetRequiredService<IQueryApiFilterState>();
        IRenderedComponent<QueryApiFilterBar> cut = Render<QueryApiFilterBar>(p => p
            .Add(c => c.LocationOptions, Locations)
            .Add(c => c.LocationElementId, "bar-location"));

        IRenderedComponent<SfDropDownList<string, QueryApiSelectOption>> dropdown = cut.FindComponent<SfDropDownList<string, QueryApiSelectOption>>();
        await cut.InvokeAsync(() => dropdown.Instance.ValueChanged.InvokeAsync("loc-1"));

        Assert.Equal("loc-1", state.LocationId);
    }
}
