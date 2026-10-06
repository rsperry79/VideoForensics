using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Circuit-scoped orchestration of the active UI layout mode (Standard/Simple),
    /// stored per-operator (OperatorPreferences, keyed by PairedSessionState.OperatorId) rather
    /// than per-device or globally. Before sign-in (no OperatorId), mode defaults to Standard and
    /// is neither persisted nor changed; once signed in, the operator's stored row is loaded (or
    /// created with defaults on first sign-in). This service mirrors <see cref="ThemePreferenceService"/>
    /// in structure and lifecycle.
    /// </summary>
    public class UiModeService : IUiModeService
    {
        private readonly IOperatorPreferencesRepository _repository;
        private readonly PairedSessionState _session;

        private bool _initialized;

        public UiModeService(
            IOperatorPreferencesRepository repository,
            PairedSessionState session)
        {
            _repository = repository;
            _session = session;
        }

        public string Mode { get; private set; } = "Standard";

        public event Action? OnChange;

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            await _session.EnsureLoadedAsync();
            if (_session.OperatorId is Guid operatorId)
            {
                OperatorPreferences? saved = await _repository.GetAsync(operatorId, CancellationToken.None);
                if (saved is not null)
                {
                    Mode = saved.UiMode ?? "Standard";
                }
            }

            OnChange?.Invoke();
        }

        public async Task SetModeAsync(string mode)
        {
            Mode = mode;
            await PersistAsync();
            OnChange?.Invoke();
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

            // Read the existing row first (if present) to avoid clobbering other fields like ThemeMode
            // or CultureName that ThemePreferenceService might have set. If no row exists yet, this
            // returns null and we create one fresh with just the UiMode set.
            OperatorPreferences? existing = await _repository.GetAsync(operatorId, CancellationToken.None);

            // Merge: keep existing ThemeMode and CultureName, but update UiMode and UpdatedAtUtc.
            var prefs = new OperatorPreferences
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                OperatorId = operatorId,
                ThemeMode = existing?.ThemeMode ?? "System",
                CultureName = existing?.CultureName,
                UiMode = Mode,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _repository.UpsertAsync(prefs, CancellationToken.None);
        }
    }
}
