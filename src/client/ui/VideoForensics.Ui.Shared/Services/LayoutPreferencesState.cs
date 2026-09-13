using Microsoft.JSInterop;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Circuit-scoped, device-local (NOT per-operator) collapse state for the docked layout's left
    /// nav rail and right settings panel - persisted to localStorage via
    /// <c>wwwroot/js/layout-prefs.js</c>. Both default to expanded. Mirrors PairedSessionState's
    /// "best-effort JS interop, tolerate JSException" pattern for hosts where the script isn't
    /// loaded yet.
    /// </summary>
    public class LayoutPreferencesState
    {
        private const string LeftNavKey = "vf.layout.leftNavCollapsed";
        private const string RightPanelKey = "vf.layout.rightPanelCollapsed";
        private const string LeftNavWidthKey = "vf.layout.leftNavWidth";
        private const string RightPanelWidthKey = "vf.layout.rightPanelWidth";

        private readonly IJSRuntime _js;
        private bool _loaded;

        public LayoutPreferencesState(IJSRuntime js)
        {
            _js = js;
        }

        public bool LeftNavCollapsed { get; private set; }
        public bool RightPanelCollapsed { get; private set; }
        public double LeftNavWidth { get; private set; }
        public double RightPanelWidth { get; private set; }

        public async Task EnsureLoadedAsync()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            try
            {
                LeftNavCollapsed = await GetBoolAsync(LeftNavKey);
                RightPanelCollapsed = await GetBoolAsync(RightPanelKey);
                LeftNavWidth = await GetDoubleAsync(LeftNavWidthKey, 220);
                RightPanelWidth = await GetDoubleAsync(RightPanelWidthKey, 280);
            }
            catch (JSException)
            {
                // Script not loaded on this host (or pre-render pass) - keep the defaults.
            }
        }

        public async Task SetLeftNavCollapsedAsync(bool collapsed)
        {
            LeftNavCollapsed = collapsed;
            try
            {
                await _js.InvokeVoidAsync("vfLayoutPrefs.set", LeftNavKey, collapsed.ToString());
            }
            catch (JSException)
            {
                // Best-effort persistence - in-memory value for this circuit still applies.
            }
        }

        public async Task SetRightPanelCollapsedAsync(bool collapsed)
        {
            RightPanelCollapsed = collapsed;
            try
            {
                await _js.InvokeVoidAsync("vfLayoutPrefs.set", RightPanelKey, collapsed.ToString());
            }
            catch (JSException)
            {
                // Best-effort persistence - in-memory value for this circuit still applies.
            }
        }

        public async Task SetLeftNavWidthAsync(double width)
        {
            LeftNavWidth = width;
            try
            {
                await _js.InvokeVoidAsync("vfLayoutPrefs.set", LeftNavWidthKey, width.ToString());
            }
            catch (JSException)
            {
                // Best-effort persistence - in-memory value for this circuit still applies.
            }
        }

        public async Task SetRightPanelWidthAsync(double width)
        {
            RightPanelWidth = width;
            try
            {
                await _js.InvokeVoidAsync("vfLayoutPrefs.set", RightPanelWidthKey, width.ToString());
            }
            catch (JSException)
            {
                // Best-effort persistence - in-memory value for this circuit still applies.
            }
        }

        private async Task<bool> GetBoolAsync(string key)
        {
            string? value = await _js.InvokeAsync<string?>("vfLayoutPrefs.get", key);
            return bool.TryParse(value, out bool result) && result;
        }

        private async Task<double> GetDoubleAsync(string key, double defaultValue)
        {
            string? value = await _js.InvokeAsync<string?>("vfLayoutPrefs.get", key);
            return double.TryParse(value, out double result) ? result : defaultValue;
        }
    }
}
