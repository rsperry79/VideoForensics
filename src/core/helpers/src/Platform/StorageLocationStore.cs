namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Storage locations read from the saved configuration in the database. Startup fills these before anything resolves a
    /// category path, because the database is opened first and its saved configuration is the source for them.
    /// Null means the value is not saved, so the provider falls back to the legacy pointer file or the default.
    /// </summary>
    public static class StorageLocationStore
    {
        /// <summary>The saved Keys folder, or null when none is saved.</summary>
        public static string? KeysRoot { get; set; }

        /// <summary>The saved Logs folder override, or null when none is saved.</summary>
        public static string? LogsRoot { get; set; }

        /// <summary>The saved default storage root, or null when none is saved.</summary>
        public static string? DefaultRoot { get; set; }

        /// <summary>
        /// True once the database was read at startup. From then on the saved values are authoritative: a null value
        /// means the category is on its default, so legacy pointer files are ignored. Pointers are read only when the
        /// database is missing or unreadable, which is the upgrade path from before the values were saved.
        /// </summary>
        public static bool SavedSettingsRead { get; set; }
    }
}
