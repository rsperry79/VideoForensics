using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Filters;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Filters;

/// <summary>The provider and account choice every page shares: it loads once, saves on change, and notifies its readers.</summary>
public class ProviderAccountFilterStateTests : BunitContext
{
    public ProviderAccountFilterStateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddScoped<RightPanelFilterStore>();
        _ = Services.AddScoped<ProviderAccountFilterState>();
    }

    [Fact]
    public async Task ProviderAccountFilterState_EnsureLoaded_RestoresStoredProvidersAndAccount()
    {
        JSInterop.Setup<string?>("vfLayoutPrefs.get", "vf.filters.provider-account")
            .SetResult("{\"providers\":[\"Ring\"],\"account\":\"abc\"}");
        ProviderAccountFilterState state = Services.GetRequiredService<ProviderAccountFilterState>();

        await state.EnsureLoadedAsync();

        Assert.Equal(new[] { "Ring" }, state.Providers);
        Assert.Equal("abc", state.Account);
    }

    [Fact]
    public async Task ProviderAccountFilterState_SetAsync_SavesAndNotifiesReaders()
    {
        ProviderAccountFilterState state = Services.GetRequiredService<ProviderAccountFilterState>();
        int notified = 0;
        state.OnChange += () => notified++;

        await state.SetAsync(["Wyze"], null);

        Assert.Equal(new[] { "Wyze" }, state.Providers);
        Assert.Null(state.Account);
        Assert.Equal(1, notified);
        _ = JSInterop.VerifyInvoke("vfLayoutPrefs.set");
    }

    [Fact]
    public async Task ProviderAccountFilterState_TwoReaders_SeeTheSameChoice()
    {
        ProviderAccountFilterState writer = Services.GetRequiredService<ProviderAccountFilterState>();
        await writer.SetAsync(["Ring"], "abc");

        // A second page resolves the same scoped instance, so it sees the same choice without loading anything.
        ProviderAccountFilterState reader = Services.GetRequiredService<ProviderAccountFilterState>();

        Assert.Same(writer, reader);
        Assert.Equal(new[] { "Ring" }, reader.Providers);
        Assert.Equal("abc", reader.Account);
    }
}
