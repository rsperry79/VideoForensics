using System.Text.Json;

using Microsoft.JSInterop;

namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>The right-panel filter choices a page remembers: the selected providers and the chosen account id (null for All accounts).</summary>
/// <param name="Cameras">Camera ids a camera page remembers. Null for pages that filter by provider and account.</param>
public sealed record RightPanelFilters(string[]? Providers, string? Account, string[]? Cameras = null);

/// <summary>
/// Device-local memory of each page's right-panel filters, kept in browser local storage through
/// <c>vfLayoutPrefs</c> so the choice survives a reload. Best-effort: when storage is unavailable
/// the page simply uses its defaults.
/// </summary>
public sealed class RightPanelFilterStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _js;

    public RightPanelFilterStore(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>Returns the page's stored filters, or null when none were stored or storage is unavailable.</summary>
    public async Task<RightPanelFilters?> LoadAsync(string pageKey)
    {
        try
        {
            string? json = await _js.InvokeAsync<string?>("vfLayoutPrefs.get", KeyFor(pageKey));
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<RightPanelFilters>(json, JsonOptions);
        }
        catch (JSException)
        {
            // Script not loaded on this host, or a pre-render pass - the page keeps its defaults.
            return null;
        }
        catch (JsonException)
        {
            // A corrupt stored value is ignored; the next save overwrites it.
            return null;
        }
    }

    public async Task SaveAsync(string pageKey, RightPanelFilters filters)
    {
        try
        {
            await _js.InvokeVoidAsync("vfLayoutPrefs.set", KeyFor(pageKey), JsonSerializer.Serialize(filters, JsonOptions));
        }
        catch (JSException)
        {
            // Best-effort persistence - the in-memory choice still applies for this circuit.
        }
    }

    private static string KeyFor(string pageKey) => "vf.filters." + pageKey;
}
