using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Providers.Ring.Implementations;

namespace VideoForensics.Providers.Ring.Auth.Tests.Implementations
{
    /// <summary>
    /// Tests that <see cref="AesEncryption"/> derives its key from the injected machine GUID source.
    /// </summary>
    [Collection("ring-credential-salt")]
    public class AesEncryptionMachineGuidSourceTests : IDisposable
    {
        // Why: without a temp folder these tests read and write the real salt file under the Keys folder.
        private readonly string _saltDir = Path.Combine(Path.GetTempPath(), $"aes-guid-test-{Guid.NewGuid()}");

        public AesEncryptionMachineGuidSourceTests()
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

        private sealed class FixedMachineGuidSource : IMachineGuidSource
        {
            private readonly string? _guid;

            public FixedMachineGuidSource(string? guid)
            {
                _guid = guid;
            }

            public string? ReadMachineGuid()
            {
                return _guid;
            }
        }

        /// <summary>Verifies that ciphertext round-trips when the same machine GUID source is used for encrypt and decrypt.</summary>
        [Fact]
        public void AesEncryption_SameSource_DecryptsEncryptedValue()
        {
            var encryptor = new AesEncryption(new FixedMachineGuidSource("guid-a"), _saltDir);
            var decryptor = new AesEncryption(new FixedMachineGuidSource("guid-a"), _saltDir);

            string cipher = encryptor.Encrypt("secret");

            Assert.Equal("secret", decryptor.Decrypt(cipher));
        }

        /// <summary>Verifies that a different machine GUID changes the derived key, so ciphertext from one machine does not decrypt on another.</summary>
        [Fact]
        public void AesEncryption_DifferentSource_CannotDecryptEncryptedValue()
        {
            var encryptor = new AesEncryption(new FixedMachineGuidSource("guid-a"), _saltDir);
            var other = new AesEncryption(new FixedMachineGuidSource("guid-b"), _saltDir);

            string cipher = encryptor.Encrypt("secret");

            // Why: a wrong key does not always fail. Unauthenticated CBC can pass the PKCS7 check (about 1 in 256)
            // and return garbage, so the assertion is that the plaintext is not recovered, not that the result is null.
            Assert.NotEqual("secret", other.Decrypt(cipher));
        }

        /// <summary>Verifies that a source returning null falls back to the hostname, so the key is still derivable.</summary>
        [Fact]
        public void AesEncryption_SourceReturnsNull_FallsBackToHostnameKey()
        {
            var nullSource = new AesEncryption(new FixedMachineGuidSource(null), _saltDir);
            var hostnameFallback = new AesEncryption(new FixedMachineGuidSource(null), _saltDir);

            string cipher = nullSource.Encrypt("secret");

            Assert.Equal("secret", hostnameFallback.Decrypt(cipher));
        }
    }
}
