using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for per-operator UI preferences (theme mode, language). One row per OperatorId.</summary>
    public interface IOperatorPreferencesRepository
    {
        /// <summary>Gets the preferences row for an operator, or null if none has been saved yet.</summary>
        Task<OperatorPreferences?> GetAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Inserts or updates the preferences row for the given operator (keyed by OperatorId).</summary>
        Task UpsertAsync(OperatorPreferences preferences, CancellationToken ct);

        /// <summary>
        /// Sets the UI mode and lock status for an operator.
        /// If the operator has no preferences row yet, creates one with default theme "System" and no culture.
        /// If a row exists, updates only the UiMode, UiModeLocked, and UpdatedAtUtc, leaving ThemeMode and CultureName unchanged.
        /// </summary>
        /// <param name="operatorId">The operator ID.</param>
        /// <param name="uiMode">The UI mode: either "Standard" or "Simple". Other values throw ArgumentException.</param>
        /// <param name="locked">When true, lock the UI mode so the operator cannot change it via UpsertAsync.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The saved OperatorPreferences row.</returns>
        /// <exception cref="ArgumentException">Thrown if uiMode is neither "Standard" nor "Simple".</exception>
        Task<OperatorPreferences> SetUiModeAsync(Guid operatorId, string uiMode, bool locked, CancellationToken ct);
    }
}
