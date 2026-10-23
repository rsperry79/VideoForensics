using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Ui.Shared.Services.SelfTest;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class SelfTestEndpointFilterTests
{
    // Resolver stub: proves labels flow through the resource lookup rather than raw keys.
    private static string Resolve(string key)
    {
        return $"[{key}]";
    }

    private static SelfTestEndpointDto Ep(string key, string provider, bool destructive = false, bool physical = false)
    {
        return new(key, key, "d", "s", "GET", "/p", "None", destructive, physical, provider);
    }

    private static readonly SelfTestEndpointDto[] Endpoints =
    [
        Ep("ring_ro", "Ring"),
        Ep("ring_destr", "Ring", destructive: true),
        Ep("wyze_ro", "Wyze"),
    ];

    [Fact]
    public void Filter_WithProvider_NarrowsToThatProvider()
    {
        IReadOnlyList<SelfTestEndpointDto> result = SelfTestEndpointFilter.Filter(Endpoints, "Wyze", null);

        Assert.Equal(["wyze_ro"], result.Select(e => e.Key));
    }

    [Fact]
    public void Filter_WithCategory_NarrowsToThatCategory()
    {
        IReadOnlyList<SelfTestEndpointDto> result = SelfTestEndpointFilter.Filter(Endpoints, null, SelfTestCategory.Destructive);

        Assert.Equal(["ring_destr"], result.Select(e => e.Key));
    }

    [Fact]
    public void CategoryOf_PhysicalEndpoint_IsDestructive()
    {
        Assert.Equal(SelfTestCategory.Destructive, SelfTestEndpointFilter.CategoryOf(Ep("p", "Ring", physical: true)));
    }

    [Fact]
    public void ProviderOptions_WithNoOtherFilter_ListsEveryProvider()
    {
        IReadOnlyList<SelfTestFilterOption> options = SelfTestEndpointFilter.ProviderOptions(Endpoints, null, Resolve);

        Assert.Equal(["Ring", "Wyze"], options.Select(o => o.Value));
    }

    [Fact]
    public void ProviderOptions_ExcludesProvidersWithNoEndpointInCategory()
    {
        IReadOnlyList<SelfTestFilterOption> options = SelfTestEndpointFilter.ProviderOptions(Endpoints, SelfTestCategory.Destructive, Resolve);

        Assert.Equal(["Ring"], options.Select(o => o.Value));
    }

    [Fact]
    public void CategoryOptions_ExcludesCategoriesWithNoEndpointForProvider()
    {
        IReadOnlyList<SelfTestFilterOption> options = SelfTestEndpointFilter.CategoryOptions(Endpoints, "Wyze", Resolve);

        Assert.Equal([SelfTestCategory.ReadOnly.ToString()], options.Select(o => o.Value));
    }

    [Fact]
    public void CategoryOptions_WithNoProvider_ListsBothCategories()
    {
        IReadOnlyList<SelfTestFilterOption> options = SelfTestEndpointFilter.CategoryOptions(Endpoints, null, Resolve);

        Assert.Equal(2, options.Count);
    }

    [Fact]
    public void ProviderOptions_Labels_AreResolvedNotRaw()
    {
        IReadOnlyList<SelfTestFilterOption> options = SelfTestEndpointFilter.ProviderOptions(Endpoints, null, Resolve);

        Assert.Equal("[Provider_Ring]", options[0].Label);
        Assert.Equal("[Provider_Wyze]", options[1].Label);
    }

    [Fact]
    public void CategoryOptions_Labels_AreResolvedNotRawEnumNames()
    {
        IReadOnlyList<SelfTestFilterOption> options = SelfTestEndpointFilter.CategoryOptions(Endpoints, null, Resolve);

        Assert.Equal("[Category_ReadOnly]", options.Single(o => o.Value == "ReadOnly").Label);
        Assert.Equal("[Category_Destructive]", options.Single(o => o.Value == "Destructive").Label);
        Assert.DoesNotContain(options, o => o.Label == o.Value);
    }

    [Fact]
    public void ProviderLabel_UnknownProvider_FallsBackToProviderName()
    {
        // The resolver echoes the key when no resource exists (IStringLocalizer behaviour).
        string label = SelfTestEndpointFilter.ProviderLabel("Acme", key => key);

        Assert.Equal("Acme", label);
    }

    [Fact]
    public void SelectAll_AddsEveryGroupKeyAndKeepsOthers()
    {
        IEnumerable<SelfTestEndpointDto> group = Endpoints.Where(e => e.Provider == "Ring" && !e.Destructive);

        string[] result = SelfTestEndpointFilter.SelectAll(["other"], group);

        Assert.Equal(["other", "ring_ro"], result.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void SelectAll_DoesNotDuplicateAlreadySelectedKeys()
    {
        string[] result = SelfTestEndpointFilter.SelectAll(["ring_ro"], [Endpoints[0]]);

        _ = Assert.Single(result);
    }

    [Fact]
    public void UnselectAll_RemovesOnlyGroupKeys()
    {
        string[] result = SelfTestEndpointFilter.UnselectAll(["ring_ro", "ring_destr"], [Endpoints[0]]);

        Assert.Equal(["ring_destr"], result);
    }
}
