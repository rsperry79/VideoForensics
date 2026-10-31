using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Ui.Shared.Components.Filters;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Filters;

/// <summary>The reusable Provider, Account and Location selects: options, selected state and the values they raise.</summary>
public class FilterSelects_Tests : BunitContext
{
    private static readonly ProviderAccount RingAccount = new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        ProviderName = "Ring",
        LinkedUtc = DateTime.UtcNow,
    };

    private static readonly ProviderAccount WyzeAccount = new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        ProviderName = "Wyze",
        LinkedUtc = DateTime.UtcNow,
    };

    private static readonly FilterLocation Porch = new(Guid.NewGuid(), "Porch");
    private static readonly FilterLocation Garage = new(Guid.NewGuid(), "Garage");

    public FilterSelects_Tests()
    {
        _ = Services.AddLocalization();
    }

    private IRenderedComponent<FilterProviderSelect> RenderProvider(string? value, Action<string?>? changed = null)
    {
        return Render<FilterProviderSelect>(p => p
            .Add(c => c.ProviderNames, ["Ring", "Wyze"])
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string?>(this, v => changed?.Invoke(v)))
            .Add(c => c.TestId, "provider-test")
            .Add(c => c.Class, "form-select custom"));
    }

    private IRenderedComponent<FilterAccountSelect> RenderAccount(Guid? value, Action<Guid?>? changed = null)
    {
        return Render<FilterAccountSelect>(p => p
            .Add(c => c.Accounts, [RingAccount, WyzeAccount])
            .Add(c => c.AccountNames, new Dictionary<Guid, string> { [RingAccount.Id] = "Front door account" })
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<Guid?>(this, v => changed?.Invoke(v)))
            .Add(c => c.TestId, "account-test"));
    }

    private IRenderedComponent<FilterLocationSelect> RenderLocation(Guid? value, Action<Guid?>? changed = null)
    {
        return Render<FilterLocationSelect>(p => p
            .Add(c => c.Locations, [Porch, Garage])
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<Guid?>(this, v => changed?.Invoke(v)))
            .Add(c => c.TestId, "location-test"));
    }

    [Fact]
    public void FilterProviderSelect_Rendered_ShowsAllOptionAndOneOptionPerProvider()
    {
        IRenderedComponent<FilterProviderSelect> cut = RenderProvider(null);

        Assert.Equal(["", "Ring", "Wyze"], cut.FindAll("[data-testid='provider-test'] option").Select(o => o.GetAttribute("value")!));
        Assert.Contains("custom", cut.Find("select").ClassName);
    }

    [Fact]
    public void FilterProviderSelect_Selected_RaisesTheProviderAndNullForAll()
    {
        List<string?> raised = [];
        IRenderedComponent<FilterProviderSelect> cut = RenderProvider("Ring", raised.Add);

        cut.Find("select").Change("Wyze");
        cut.Find("select").Change("");

        Assert.Equal(["Wyze", null], raised);
    }

    [Fact]
    public void FilterProviderSelect_Value_MarksTheMatchingOptionSelected()
    {
        IRenderedComponent<FilterProviderSelect> cut = RenderProvider("Wyze");

        Assert.Equal(["Wyze"], cut.FindAll("option").Where(o => o.HasAttribute("selected")).Select(o => o.GetAttribute("value")!));
    }

    [Fact]
    public void FilterAccountSelect_Rendered_ShowsAllOptionAndNamedAccountsWithProviderNameFallback()
    {
        IRenderedComponent<FilterAccountSelect> cut = RenderAccount(null);

        Assert.Equal(["", RingAccount.Id.ToString(), WyzeAccount.Id.ToString()], cut.FindAll("[data-testid='account-test'] option").Select(o => o.GetAttribute("value")!));
        Assert.Equal(["Front door account", "Wyze"], cut.FindAll("option").Skip(1).Select(o => o.TextContent));
    }

    [Fact]
    public void FilterAccountSelect_Selected_RaisesTheAccountAndNullForAll()
    {
        List<Guid?> raised = [];
        IRenderedComponent<FilterAccountSelect> cut = RenderAccount(null, raised.Add);

        cut.Find("select").Change(WyzeAccount.Id.ToString());
        cut.Find("select").Change("");

        Assert.Equal([WyzeAccount.Id, (Guid?)null], raised);
    }

    [Fact]
    public void FilterAccountSelect_Value_MarksTheMatchingOptionSelected()
    {
        IRenderedComponent<FilterAccountSelect> cut = RenderAccount(WyzeAccount.Id);

        Assert.Equal([WyzeAccount.Id.ToString()], cut.FindAll("option").Where(o => o.HasAttribute("selected")).Select(o => o.GetAttribute("value")!));
    }

    [Fact]
    public void FilterLocationSelect_Rendered_ShowsAllOptionAndOneOptionPerLocation()
    {
        IRenderedComponent<FilterLocationSelect> cut = RenderLocation(null);

        Assert.Equal(["", Porch.Id.ToString(), Garage.Id.ToString()], cut.FindAll("[data-testid='location-test'] option").Select(o => o.GetAttribute("value")!));
        Assert.Equal(["Porch", "Garage"], cut.FindAll("option").Skip(1).Select(o => o.TextContent));
    }

    [Fact]
    public void FilterLocationSelect_Selected_RaisesTheLocationAndNullForAll()
    {
        List<Guid?> raised = [];
        IRenderedComponent<FilterLocationSelect> cut = RenderLocation(null, raised.Add);

        cut.Find("select").Change(Garage.Id.ToString());
        cut.Find("select").Change("");

        Assert.Equal([Garage.Id, (Guid?)null], raised);
    }

    [Fact]
    public void FilterLocationSelect_Value_MarksTheMatchingOptionSelected()
    {
        IRenderedComponent<FilterLocationSelect> cut = RenderLocation(Porch.Id);

        Assert.Equal([Porch.Id.ToString()], cut.FindAll("option").Where(o => o.HasAttribute("selected")).Select(o => o.GetAttribute("value")!));
    }
}
