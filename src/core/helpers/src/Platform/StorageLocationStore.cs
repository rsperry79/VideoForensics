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
    }
}
