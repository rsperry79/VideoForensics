namespace VideoForensics.Providers.Ring.Auth
{
    /// <summary>
    /// Platform-agnostic credential encryption interface.
    /// Implementations handle platform-specific encryption details while providing
    /// a consistent interface for encrypting and decrypting sensitive credentials.
    /// </summary>
    public interface ICredentialEncryption
    {
        /// <summary>
        /// Encrypts plaintext credential data to an encrypted representation.
        /// </summary>
        /// <param name="plaintext">The plaintext credential data to encrypt.</param>
        /// <returns>An encrypted representation suitable for storage.</returns>
        string Encrypt(string plaintext);

        /// <summary>
        /// Decrypts encrypted credential data back to plaintext.
        /// </summary>
        /// <param name="ciphertext">The encrypted credential data.</param>
        /// <returns>The decrypted plaintext, or null if decryption fails.</returns>
        string Decrypt(string ciphertext);
    }
}
