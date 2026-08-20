namespace Ring.Api
{
    /// <summary>
    /// Reads and writes Ring account credentials (username, password, refresh token) to a JSON file
    /// of the caller's choosing, encrypted with a key derived from the current machine and user
    /// account so the file is only readable on the machine/account that wrote it. Callers only need
    /// to supply a file path; the implementation owns the storage format and encryption.
    /// </summary>
    public interface ICredentialStore
    {
        /// <summary>
        /// Loads credentials from the given file path. Returns an empty <see cref="RingCredentials"/>
        /// if the file doesn't exist or can't be decrypted (e.g. written on a different machine/account).
        /// </summary>
        RingCredentials Load(string path);

        /// <summary>
        /// Decrypts credentials from a raw JSON string in the storage format written by <see cref="Save"/>.
        /// Returns an empty <see cref="RingCredentials"/> if the JSON is malformed or can't be decrypted.
        /// </summary>
        RingCredentials LoadFromJson(string json);

        /// <summary>
        /// Encrypts and saves credentials to the given file path, creating the parent directory if needed.
        /// </summary>
        void Save(string path, RingCredentials credentials);

        /// <summary>
        /// Convenience method to set credentials without callers hand-building RingCredentials.
        /// </summary>
        void SetCredentials(string path, string userName, string password = null, string refreshToken = null);

        /// <summary>
        /// Scans <paramref name="filePath"/> for a top-level clear-text <paramref name="clearFieldName"/>
        /// property. If found and non-empty, migrates it into the encrypted credential store at
        /// <paramref name="authPath"/> (merging with whatever's already there) via Save, then removes the
        /// clear-text property from the source file. Returns true if a migration happened.
        /// </summary>
        bool SanitizeClearTextPassword(string filePath, string authPath, string clearFieldName = "Password");
    }
}
