using Microsoft.AspNetCore.DataProtection;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Data.Database.Configurations
{
    /// <summary>Credential encryption provider using Microsoft.AspNetCore.DataProtection.</summary>
    public class CredentialEncryptionProvider : ICredentialEncryptionProvider
    {
        private readonly IDataProtector _protector;

        /// <summary>Initializes a new instance of the CredentialEncryptionProvider.</summary>
        public CredentialEncryptionProvider(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("VideoForensics.Credentials");
        }

        /// <summary>Encrypts a plaintext value.</summary>
        public Task<string> EncryptAsync(string plainValue, CancellationToken ct)
        {
            var encrypted = _protector.Protect(plainValue);
            return Task.FromResult(encrypted);
        }

        /// <summary>Decrypts an encrypted value.</summary>
        public Task<string> DecryptAsync(string encryptedValue, CancellationToken ct)
        {
            var decrypted = _protector.Unprotect(encryptedValue);
            return Task.FromResult(decrypted);
        }
    }
}
