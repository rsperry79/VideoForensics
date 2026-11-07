using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Syncfusion.Blazor.DropDowns;

using VideoForensics.Client.Ui.Shared.Components.Filters;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Filters;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Filters;

/// <summary>The shared provider and account control: it shows the stored choice and keeps the account consistent with the selected providers.</summary>
public class ProviderAccountFilterTests : BunitContext
{
    private const string StorageKey = "vf.filters.provider-account";
    private const string RingAccount = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string WyzeAccount = "aaaaaaaa-0000-0000-0000-000000000002";

    private static readonly Func<string, IEnumerable<KeyValuePair<string, string>>> AccountsForProvider = AccountsFor;

    public ProviderAccountFilterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped<RightPanelFilterStore>();
        _ = Services.AddScoped<ProviderAccountFilterState>();
    }

    private static IEnumerable<KeyValuePair<string, string>> AccountsFor(string provider)
    {
        return provider switch
        {
            "Ring" => new[] { new KeyValuePair<string, string>(RingAccount, "Ring Owner") },
            "Wyze" => new[] { new KeyValuePair<string, string>(WyzeAccount, "Wyze Owner") },
            _ => Array.Empty<KeyValuePair<string, string>>(),
        };
    }

    private void StoreChoice(string json)
    {
        _ = JSInterop.Setup<string?>("vfLayoutPrefs.get", StorageKey).SetResult(json);
    }

    private IRenderedComponent<ProviderAccountFilter> RenderFilter()
    {
        return Render<ProviderAccountFilter>(parameters => parameters
            .Add(c => c.ProviderNames, new[] { "Ring", "Wyze" })
            .Add(c => c.AccountsFor, AccountsForProvider));
    }

    [Fact]
    public void ProviderAccountFilter_StoredChoice_ShowsStoredProviderAndAccount()
    {
        StoreChoice("{\"providers\":[\"Ring\"],\"account\":\"" + RingAccount + "\"}");

        IRenderedComponent<ProviderAccountFilter> cut = RenderFilter();

        cut.WaitForAssertion(() =>
        {
            IRenderedComponent<SfMultiSelect<string[], string>> providers = cut.FindComponent<SfMultiSelect<string[], string>>();
            Assert.Equal(new[] { "Ring" }, providers.Instance.Value);

            IRenderedComponent<SfDropDownList<string, KeyValuePair<string, string>>> account = cut.FindComponent<SfDropDownList<string, KeyValuePair<string, string>>>();
            Assert.Equal(RingAccount, account.Instance.Value);
        });
    }

    [Fact]
    public async Task ProviderAccountFilter_ProviderChangeExcludingAccount_ClearsAccount()
    {
        StoreChoice("{\"providers\":[\"Ring\",\"Wyze\"],\"account\":\"" + WyzeAccount + "\"}");
        IRenderedComponent<ProviderAccountFilter> cut = RenderFilter();
        ProviderAccountFilterState state = Services.GetRequiredService<ProviderAccountFilterState>();
        cut.WaitForAssertion(() => Assert.Equal(WyzeAccount, state.Account));

        IRenderedComponent<SfMultiSelect<string[], string>> providers = cut.FindComponent<SfMultiSelect<string[], string>>();
        await cut.InvokeAsync(() => providers.Instance.ValueChanged.InvokeAsync(new[] { "Ring" }));

        Assert.Equal(new[] { "Ring" }, state.Providers);
        Assert.Null(state.Account);
    }

    [Fact]
    public async Task ProviderAccountFilter_ProviderChangeKeepingAccount_KeepsAccount()
    {
        StoreChoice("{\"providers\":[\"Ring\",\"Wyze\"],\"account\":\"" + RingAccount + "\"}");
        IRenderedComponent<ProviderAccountFilter> cut = RenderFilter();
        ProviderAccountFilterState state = Services.GetRequiredService<ProviderAccountFilterState>();
        cut.WaitForAssertion(() => Assert.Equal(RingAccount, state.Account));

        IRenderedComponent<SfMultiSelect<string[], string>> providers = cut.FindComponent<SfMultiSelect<string[], string>>();
        await cut.InvokeAsync(() => providers.Instance.ValueChanged.InvokeAsync(new[] { "Ring" }));

        Assert.Equal(new[] { "Ring" }, state.Providers);
        Assert.Equal(RingAccount, state.Account);
    }
}
