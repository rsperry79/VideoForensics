using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoForensics.Ui.Shared.Resources;

namespace VideoForensics.Ui.Shared.Tests;

/// <summary>
/// Builds a real <see cref="IStringLocalizer{SharedResources}"/> backed by the shipped SharedResources.resx,
/// so tests assert the actual English strings users see (and fail if a resource key is missing or misspelt,
/// since a missing key would surface as the raw key name).
/// </summary>
internal static class TestLocalizer
{
    public static IStringLocalizer<SharedResources> Create()
    {
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
        return new StringLocalizer<SharedResources>(factory);
    }
}

/// <summary>
/// Localizer that tags every lookup as "L:Key" (or "L:Key|arg1|arg2"), proving a component or formatter routes
/// its user-visible text through the localizer rather than hard-coding English.
/// </summary>
internal sealed class TaggingLocalizer : IStringLocalizer<SharedResources>
{
    public LocalizedString this[string name] => new(name, "L:" + name);

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, "L:" + name + string.Concat(arguments.Select(a => "|" + a)));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
}