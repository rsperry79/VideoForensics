using System.Security.Cryptography;

using VideoForensics.Providers.Ring.Implementations;

namespace VideoForensics.Providers.Ring.Auth.Tests.Implementations
{
    /// <summary>
    /// The non-Windows credential cipher must authenticate its ciphertext. CBC without a MAC returns garbage for a
    /// changed or wrong-key value, and a stored value in the old format must not decrypt at all (the user re-enters it).
    /// </summary>
    [Collection("ring-credential-salt")]
    public class AesEncryptionAuthenticatedTests : IDisposable
    {
        // Why: a shared salt folder would make these tests read and write the real salt.
        private readonly string _saltDir = Path.Combine(Path.GetTempPath(), $"aes-auth-test-{Guid.NewGuid()}");

        public AesEncryptionAuthenticatedTests()
        {
            _ = Directory.CreateDirectory(_saltDir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_saltDir, true);
            }
            catch
            {
                // Swallow cleanup errors
            }
        }

        [Fact]
        public void AesEncryption_Encrypt_WritesTheAuthenticatedFormatVersion()
        {
            var aes = new AesEncryption(saltDirectory: _saltDir);

            string cipher = aes.Encrypt("secret");

            Assert.StartsWith("v2:", cipher, StringComparison.Ordinal);
        }

        [Fact]
        public void AesEncryption_TamperedCiphertext_ReturnsNull()
        {
            var aes = new AesEncryption(saltDirectory: _saltDir);
            string cipher = aes.Encrypt("secret");
            byte[] payload = Convert.FromBase64String(cipher["v2:".Length..]);
            payload[^1] ^= 0x01;
            string tampered = "v2:" + Convert.ToBase64String(payload);

            Assert.Null(aes.Decrypt(tampered));
        }

        [Fact]
        public void AesEncryption_ValueWithoutVersionPrefix_ReturnsNull()
        {
            // Why: values written by the old CBC format have no prefix and must not decrypt as if they were current.
            var aes = new AesEncryption(saltDirectory: _saltDir);
            string legacy = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

            Assert.Null(aes.Decrypt(legacy));
        }

        [Fact]
        public void AesEncryption_RoundTrip_WithAuthenticatedFormat_RecoversPlaintext()
        {
            var aes = new AesEncryption(saltDirectory: _saltDir);

            Assert.Equal("secret", aes.Decrypt(aes.Encrypt("secret")));
        }
    }
}
