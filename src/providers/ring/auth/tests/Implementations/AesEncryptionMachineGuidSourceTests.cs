using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Providers.Ring.Implementations;

using Xunit;

namespace VideoForensics.Providers.Ring.Auth.Tests
{
    /// <summary>
    /// Tests that <see cref="AesEncryption"/> derives its key from the injected machine GUID source.
    /// </summary>
    [Collection("ring-credential-salt")]
    public class AesEncryptionMachineGuidSourceTests
    {
        private sealed class FixedMachineGuidSource : IMachineGuidSource
        {
            private readonly string? _guid;

            public FixedMachineGuidSource(string? guid)
            {
                _guid = guid;
            }

            public string? ReadMachineGuid() => _guid;
        }

        /// <summary>Verifies that ciphertext round-trips when the same machine GUID source is used for encrypt and decrypt.</summary>
        [Fact]
        public void AesEncryption_SameSource_DecryptsEncryptedValue()
        {
            var encryptor = new AesEncryption(new FixedMachineGuidSource("guid-a"));
            var decryptor = new AesEncryption(new FixedMachineGuidSource("guid-a"));

            string cipher = encryptor.Encrypt("secret");

            Assert.Equal("secret", decryptor.Decrypt(cipher));
        }

        /// <summary>Verifies that a different machine GUID changes the derived key, so ciphertext from one machine does not decrypt on another.</summary>
        [Fact]
        public void AesEncryption_DifferentSource_CannotDecryptEncryptedValue()
        {
            var encryptor = new AesEncryption(new FixedMachineGuidSource("guid-a"));
            var other = new AesEncryption(new FixedMachineGuidSource("guid-b"));

            string cipher = encryptor.Encrypt("secret");

            Assert.Null(other.Decrypt(cipher));
        }

        /// <summary>Verifies that a source returning null falls back to the hostname, so the key is still derivable.</summary>
        [Fact]
        public void AesEncryption_SourceReturnsNull_FallsBackToHostnameKey()
        {
            var nullSource = new AesEncryption(new FixedMachineGuidSource(null));
            var hostnameFallback = new AesEncryption(new FixedMachineGuidSource(null));

            string cipher = nullSource.Encrypt("secret");

            Assert.Equal("secret", hostnameFallback.Decrypt(cipher));
        }
    }
}
