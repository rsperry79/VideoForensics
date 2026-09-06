namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Per-operator UI preferences (theme mode, language) - keyed by OperatorId (FK to
    /// <see cref="Operator"/>, mirroring <see cref="PairedDevice"/>.OperatorId), one row per operator.
    /// Deliberately separate from the device-local <c>IAppLockPreferencesStore</c> and the global
    /// <see cref="AppSetting"/> table: a preference here travels with the person across devices,
    /// same as <see cref="OperatorRole"/> already does.
    /// </summary>
    public class OperatorPreferences
    {
        public Guid Id { get; set; }
        public Guid OperatorId { get; set; }

        /// <summary>"Light", "Dark", or "System" (default).</summary>
        public string ThemeMode { get; set; } = "System";

        /// <summary>BCP-47 culture name (e.g. "en-US"), or null if not yet set.</summary>
        public string? CultureName { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}
