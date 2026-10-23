using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Ui.Shared.Services.SelfTest;

/// <summary>Which group an endpoint belongs to on the API Tester page.</summary>
public enum SelfTestCategory
{
    ReadOnly,
    Destructive,
}

/// <summary>One dropdown entry: the stable <see cref="Value"/> and the localized, human-readable <see cref="Label"/>.</summary>
public sealed record SelfTestFilterOption(string Value, string Label);

/// <summary>
/// Pure option-derivation and selection logic for the API Tester filters. Each filter's options are computed
/// from the endpoints that match the OTHER active filters, so an option never leads to an empty list.
/// </summary>
public static class SelfTestEndpointFilter
{
    /// <summary>Destructive and physical endpoints share the destructive group.</summary>
    public static SelfTestCategory CategoryOf(SelfTestEndpointDto endpoint)
    {
        return endpoint.Destructive || endpoint.Physical ? SelfTestCategory.Destructive : SelfTestCategory.ReadOnly;
    }

    /// <summary>Endpoints matching every non-null filter.</summary>
    public static IReadOnlyList<SelfTestEndpointDto> Filter(IEnumerable<SelfTestEndpointDto> endpoints, string? provider, SelfTestCategory? category)
    {
        return endpoints
            .Where(e => provider is null || e.Provider == provider)
            .Where(e => category is null || CategoryOf(e) == category)
            .ToList();
    }

    /// <summary>Providers that have at least one endpoint in <paramref name="category"/> (or any, when null).</summary>
    public static IReadOnlyList<SelfTestFilterOption> ProviderOptions(IEnumerable<SelfTestEndpointDto> endpoints, SelfTestCategory? category, Func<string, string> resolve)
    {
        return Filter(endpoints, null, category)
            .Select(e => e.Provider)
            .Distinct(StringComparer.Ordinal)
            .Select(p => new SelfTestFilterOption(p, ProviderLabel(p, resolve)))
            .ToList();
    }

    /// <summary>Categories that have at least one endpoint for <paramref name="provider"/> (or any, when null).</summary>
    public static IReadOnlyList<SelfTestFilterOption> CategoryOptions(IEnumerable<SelfTestEndpointDto> endpoints, string? provider, Func<string, string> resolve)
    {
        return Filter(endpoints, provider, null)
            .Select(CategoryOf)
            .Distinct()
            .OrderBy(c => c)
            .Select(c => new SelfTestFilterOption(c.ToString(), CategoryLabel(c, resolve)))
            .ToList();
    }

    /// <summary>Resource key <c>Provider_{name}</c>; a provider with no resource shows its own name.</summary>
    public static string ProviderLabel(string provider, Func<string, string> resolve)
    {
        string key = $"Provider_{provider}";
        string label = resolve(key);
        return string.IsNullOrEmpty(label) || label == key ? provider : label;
    }

    public static string CategoryLabel(SelfTestCategory category, Func<string, string> resolve)
    {
        return resolve($"Category_{category}");
    }

    /// <summary>Adds every key of <paramref name="group"/> to <paramref name="current"/>, keeping other keys.</summary>
    public static string[] SelectAll(IEnumerable<string> current, IEnumerable<SelfTestEndpointDto> group)
    {
        return current.Concat(group.Select(e => e.Key)).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Removes every key of <paramref name="group"/> from <paramref name="current"/>, keeping other keys.</summary>
    public static string[] UnselectAll(IEnumerable<string> current, IEnumerable<SelfTestEndpointDto> group)
    {
        var groupKeys = group.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        return current.Where(k => !groupKeys.Contains(k)).ToArray();
    }
}
