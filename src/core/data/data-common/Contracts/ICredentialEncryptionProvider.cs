namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Provider for encrypting and decrypting credential values, abstracting away vendor SDK details.</summary>
    public interface ICredentialEncryptionProvider
    {
        /// <summary>Encrypts a plaintext value.</summary>
        Task<string> EncryptAsync(string plainValue, CancellationToken ct);

        /// <summary>Decrypts an encrypted value.</summary>
        Task<string> DecryptAsync(string encryptedValue, CancellationToken ct);
    }
}
