using Microsoft.JSInterop;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Time;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services
{
    public enum ThemeMode
    {
        Light,
        Dark,
        System
    }

    /// <summary>
    /// Circuit-scoped orchestration of the effective Light/Dark Syncfusion theme and the active culture,
    /// both stored per-operator (OperatorPreferences, keyed by PairedSessionState.OperatorId) rather
    /// than per-device or globally. Before sign-in (no OperatorId), theme defaults to System and
    /// culture is left at whatever the host's ambient culture already is - neither is persisted server-side,
    /// since there is no operator to key a row by; once signed in, the operator's stored row is
    /// loaded (or created with defaults on first sign-in).
    /// The theme mode is also kept in browser local storage on this device, so the first paint
    /// matches the last session; the operator's row overrides that copy whenever it is read.
    /// The 12-hour or 24-hour clock is kept the same way, so every displayed time follows it.
    /// </summary>
    public class ThemePreferenceService
    {
        private readonly IJSRuntime _js;
        private readonly IOperatorPreferencesRepository _repository;
        private readonly PairedSessionState _session;
        private readonly ICultureSwitcher _cultureSwitcher;

        private bool _initialized;
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

        /// <summary>The clock every displayed time uses. <see cref="TimeFormatPreference.Locale"/> keeps the culture's own clock.</summary>
        public TimeFormatPreference TimeFormat { get; private set; } = TimeFormatPreference.Locale;

        public bool EffectiveIsDark
        {
            get => Mode switch
            {
                ThemeMode.Light => false,
                ThemeMode.Dark => true,
                _ => field
            }; private set;
        }

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
                EffectiveIsDark = await _js.InvokeAsync<bool>("vfTheme.getPrefersDark");
                _selfRef = DotNetObjectReference.Create(this);
                await _js.InvokeVoidAsync("vfTheme.watchPrefersDark", _selfRef);
            }
            catch (JSException)
            {
                // Script not loaded on this host / pre-render pass - System mode falls back to Light.
            }

            // The device-local copy is the first guess, so the render matches the last session before the
            // operator's row loads. The operator's stored row is authoritative once it is read.
            string? localMode = await ReadStoredModeAsync();
            if (Enum.TryParse(localMode, out ThemeMode cached))
            {
                Mode = cached;
            }

            string? localTimeFormat = await ReadStoredTimeFormatAsync();
            if (Enum.TryParse(localTimeFormat, out TimeFormatPreference cachedClock))
            {
                TimeFormat = cachedClock;
            }

            await _session.EnsureLoadedAsync();
            if (_session.OperatorId is Guid operatorId)
            {
                OperatorPreferences? saved = await _repository.GetAsync(operatorId, CancellationToken.None);
                if (saved is not null)
                {
                    Mode = Enum.TryParse<ThemeMode>(saved.ThemeMode, out ThemeMode parsed) ? parsed : ThemeMode.System;
                    CultureName = saved.CultureName;
                    TimeFormat = Enum.TryParse<TimeFormatPreference>(saved.TimeFormat, out TimeFormatPreference clock) ? clock : TimeFormatPreference.Locale;

                    // Out of sync with the operator's stored choice (another device, or a changed default): correct the local copy.
                    if (!string.Equals(localMode, Mode.ToString(), StringComparison.Ordinal))
                    {
                        await WriteStoredModeAsync(Mode);
                    }

                    if (!string.Equals(localTimeFormat, TimeFormat.ToString(), StringComparison.Ordinal))
                    {
                        await WriteStoredTimeFormatAsync(TimeFormat);
                    }
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
            await WriteStoredModeAsync(mode);
            await PersistAsync();
            OnChange?.Invoke();
            _ = ApplyStylesheetAsync();
        }

        public async Task SetTimeFormatAsync(TimeFormatPreference clock)
        {
            TimeFormat = clock;
            await WriteStoredTimeFormatAsync(clock);
            await PersistAsync();
            OnChange?.Invoke();
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
            EffectiveIsDark = isDark;
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

            // Read the existing row first (if present) to avoid clobbering other fields like UiMode
            // that UiModeService might have set. If no row exists yet, this returns null and we create
            // one fresh with just the theme/culture set.
            OperatorPreferences? existing = await _repository.GetAsync(operatorId, CancellationToken.None);

            // Merge: keep existing UiMode, but update ThemeMode and CultureName.
            var prefs = new OperatorPreferences
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = Mode.ToString(),
                CultureName = CultureName,
                TimeFormat = TimeFormat.ToString(),
                UiMode = existing?.UiMode ?? "Standard",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(prefs, CancellationToken.None);
        }

        private async Task<string?> ReadStoredModeAsync()
        {
            try
            {
                return await _js.InvokeAsync<string?>("vfTheme.getStoredMode");
            }
            catch (JSException)
            {
                // Script not loaded on this host / pre-render pass - the device copy is simply unknown.
                return null;
            }
        }

        private async Task WriteStoredModeAsync(ThemeMode mode)
        {
            try
            {
                await _js.InvokeVoidAsync("vfTheme.setStoredMode", mode.ToString());
            }
            catch (JSException)
            {
                // Best-effort - the in-memory choice still applies for this circuit.
            }
        }

        private async Task<string?> ReadStoredTimeFormatAsync()
        {
            try
            {
                return await _js.InvokeAsync<string?>("vfTheme.getStoredTimeFormat");
            }
            catch (JSException)
            {
                // Script not loaded on this host / pre-render pass - the device copy is simply unknown.
                return null;
            }
        }

        private async Task WriteStoredTimeFormatAsync(TimeFormatPreference clock)
        {
            try
            {
                await _js.InvokeVoidAsync("vfTheme.setStoredTimeFormat", clock.ToString());
            }
            catch (JSException)
            {
                // Best-effort - the in-memory choice still applies for this circuit.
            }
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
