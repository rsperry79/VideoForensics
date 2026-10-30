using System.Text.Json;

using Microsoft.JSInterop;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// The settings Media and the Review pages share: the review URL query (cameras, range, filters, case, tab) and the
/// selected event.
/// </summary>
/// <param name="Query">The query in the form <see cref="ReviewUrl.Build"/> writes and <see cref="ReviewUrl.Parse"/> reads.</param>
/// <param name="EventId">The selected event, or null.</param>
public sealed record SavedReviewSettings(string Query, Guid? EventId);

/// <summary>
/// Keeps <see cref="SavedReviewSettings"/> in the browser's localStorage so the review view survives a reload and
/// follows the operator between Media and the Review pages. It is a per-browser convenience: a store that is missing,
/// unreadable or blocked just means the view starts from its defaults.
/// </summary>
public sealed class ReviewSettingsStore
{
    /// <summary>The localStorage key the settings are kept under.</summary>
    public const string StorageKey = "vf.review.state";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _js;

    /// <summary>Creates the store over the circuit's JS runtime.</summary>
    public ReviewSettingsStore(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>Reads the saved settings, or null when none are saved or they cannot be read.</summary>
    public async Task<SavedReviewSettings?> LoadAsync()
    {
        try
        {
            string? json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<SavedReviewSettings>(json, Options);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Replaces the saved settings. A failure is ignored: only the next reload's restore is lost.</summary>
    public async Task SaveAsync(SavedReviewSettings settings)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            // Nothing to do: the page keeps working without the saved copy.
        }
    }

    /// <summary>Loads the saved settings (empty ones when none), applies <paramref name="change"/>, and saves the result.</summary>
    public async Task UpdateAsync(Func<SavedReviewSettings, SavedReviewSettings> change)
    {
        SavedReviewSettings current = await LoadAsync() ?? new SavedReviewSettings(string.Empty, null);
        await SaveAsync(change(current));
    }
}
