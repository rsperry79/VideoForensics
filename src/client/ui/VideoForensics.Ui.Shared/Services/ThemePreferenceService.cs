using Microsoft.JSInterop;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services
{
    public enum ThemeMode
    {
        Light,
        Dark,
        System
    }

    /// <summary>
    /// Circuit-scoped orchestration of the effective Light/Dark Radzen theme and the active culture,
    /// both stored per-operator (OperatorPreferences, keyed by PairedSessionState.OperatorId) rather
    /// than per-device or globally. Before sign-in (no OperatorId), theme defaults to System and
    /// culture is left at whatever the host's ambient culture already is - neither is persisted,
    /// since there is no operator to key a row by; once signed in, the operator's stored row is
    /// loaded (or created with defaults on first sign-in).
    /// </summary>
    public class ThemePreferenceService
    {
        private readonly IJSRuntime _js;
        private readonly IOperatorPreferencesRepository _repository;
        private readonly PairedSessionState _session;
        private readonly ICultureSwitcher _cultureSwitcher;

        private bool _initialized;
        private bool _prefersDark;
        private DotNetObjectReference<ThemePreferenceService>? _selfRef;

        public ThemePreferenceService(
            IJSRuntime js,
            IOperatorPreferencesRepository repository,
            PairedSessionState session,
            ICultureSwitcher cultureSwitcher)
        {
            _js = js;
            _repository = repository;
            _session = session;
            _cultureSwitcher = cultureSwitcher;
        }

        public ThemeMode Mode { get; private set; } = ThemeMode.System;
        public string? CultureName { get; private set; }

        public bool EffectiveIsDark => Mode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => _prefersDark
        };

        public event Action? OnChange;

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                _prefersDark = await _js.InvokeAsync<bool>("vfTheme.getPrefersDark");
                _selfRef = DotNetObjectReference.Create(this);
                await _js.InvokeVoidAsync("vfTheme.watchPrefersDark", _selfRef);
            }
            catch (JSException)
            {
                // Script not loaded on this host / pre-render pass - System mode falls back to Light.
            }

            await _session.EnsureLoadedAsync();
            if (_session.OperatorId is Guid operatorId)
            {
                OperatorPreferences? saved = await _repository.GetAsync(operatorId, CancellationToken.None);
                if (saved is not null)
                {
                    Mode = Enum.TryParse<ThemeMode>(saved.ThemeMode, out ThemeMode parsed) ? parsed : ThemeMode.System;
                    CultureName = saved.CultureName;
                }
            }

            if (CultureName is not null)
            {
                _cultureSwitcher.Apply(CultureName);
            }

            OnChange?.Invoke();
            _ = ApplyStylesheetAsync();
        }

        public async Task SetModeAsync(ThemeMode mode)
        {
            Mode = mode;
            await PersistAsync();
            OnChange?.Invoke();
            _ = ApplyStylesheetAsync();
        }

        public async Task SetCultureAsync(string cultureName)
        {
            CultureName = cultureName;
            _cultureSwitcher.Apply(cultureName);
            await PersistAsync();
            OnChange?.Invoke();
        }

        [JSInvokable]
        public void OnSystemThemeChanged(bool isDark)
        {
            _prefersDark = isDark;
            if (Mode == ThemeMode.System)
            {
                OnChange?.Invoke();
                _ = ApplyStylesheetAsync();
            }
        }

        private async Task PersistAsync()
        {
            await _session.EnsureLoadedAsync();
            if (_session.OperatorId is not Guid operatorId)
            {
                // No signed-in operator - nothing to key a row by. The in-memory choice above still
                // applies for the rest of this session; it's simply not persisted (see class doc).
                return;
            }

            await _repository.UpsertAsync(new OperatorPreferences
            {
                OperatorId = operatorId,
                ThemeMode = Mode.ToString(),
                CultureName = CultureName
            }, CancellationToken.None);
        }

        private async Task ApplyStylesheetAsync()
        {
            try
            {
                await _js.InvokeVoidAsync("vfTheme.setStylesheet", EffectiveIsDark);
            }
            catch (JSException)
            {
                // Script not loaded on this host / pre-render pass - page keeps whatever theme was last successfully applied.
            }
        }
    }
}
