using Microsoft.JSInterop;

namespace VideoForensics.Client.Ui.Shared.Services
{
    /// <summary>Pure rule for which linked account is the current selection.</summary>
    public static class AccountSelection
    {
        /// <summary>
        /// Returns the stored account when it is still linked; otherwise the first linked account (or null
        /// when none exist). A stored value that does not parse as a GUID is treated as "not set".
        /// </summary>
        public static Guid? Resolve(string? storedId, IReadOnlyList<Guid> accountIds)
        {
            return accountIds.Count == 0
                ? null
                : Guid.TryParse(storedId, out Guid stored) && accountIds.Contains(stored) ? stored : accountIds[0];
        }
    }

    /// <summary>
    /// Browser-local (per-viewer) memory of the currently selected provider account, kept in localStorage via
    /// <c>wwwroot/js/layout-prefs.js</c>. Deliberately not server state: each browser remembers its own choice.
    /// Best-effort - if storage is unavailable the first linked account is used.
    /// </summary>
    public class AccountSelectionState
    {
        private const string SelectedAccountKey = "vf.account.selected";

        private readonly IJSRuntime _js;

        public AccountSelectionState(IJSRuntime js)
        {
            _js = js;
        }

        /// <summary>Resolves the selection for the given linked accounts (see <see cref="AccountSelection.Resolve"/>).</summary>
        public async Task<Guid?> GetSelectedAccountIdAsync(IReadOnlyList<Guid> accountIds)
        {
            string? stored = null;
            try
            {
                stored = await _js.InvokeAsync<string?>("vfLayoutPrefs.get", SelectedAccountKey);
            }
            catch (JSException)
            {
                // Script not loaded on this host (or pre-render pass) - fall back to the first account.
            }

            return AccountSelection.Resolve(stored, accountIds);
        }

        /// <summary>Remembers the selection in this browser.</summary>
        public async Task SetSelectedAccountIdAsync(Guid accountId)
        {
            try
            {
                await _js.InvokeVoidAsync("vfLayoutPrefs.set", SelectedAccountKey, accountId.ToString());
            }
            catch (JSException)
            {
                // Best-effort - the selection simply isn't remembered.
            }
        }
    }
}
