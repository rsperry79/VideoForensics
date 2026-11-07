namespace VideoForensics.Client.Ui.Shared.Services.Filters;

/// <summary>
/// The provider and account choice that every page with a Provider or Account filter shares. One scoped instance holds
/// the choice, so a change on one page is what the others show. It loads once from device-local storage and saves each change.
/// </summary>
public sealed class ProviderAccountFilterState
{
    private const string StorageKey = "provider-account";

    private readonly RightPanelFilterStore _store;
    private bool _loaded;

    public ProviderAccountFilterState(RightPanelFilterStore store)
    {
        _store = store;
    }

    /// <summary>The selected providers. Empty means every provider.</summary>
    public string[] Providers { get; private set; } = [];

    /// <summary>The selected account id as text, or null for All accounts.</summary>
    public string? Account { get; private set; }

    /// <summary>Raised after the choice changes, so each page that shows it re-filters.</summary>
    public event Action? OnChange;

    /// <summary>Reads the stored choice once. Later calls do nothing, so a page can call it on every init.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        RightPanelFilters? stored = await _store.LoadAsync(StorageKey);
        if (stored is null)
        {
            return;
        }

        Providers = stored.Providers ?? [];
        Account = stored.Account;
        OnChange?.Invoke();
    }

    /// <summary>Sets the choice, saves it, and tells every reader.</summary>
    public async Task SetAsync(string[] providers, string? account)
    {
        Providers = providers ?? [];
        Account = account;
        await _store.SaveAsync(StorageKey, new RightPanelFilters(Providers, Account));
        OnChange?.Invoke();
    }
}
