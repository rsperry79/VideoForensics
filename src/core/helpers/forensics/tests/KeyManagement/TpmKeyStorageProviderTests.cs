using System.Text;

using VideoForensics.Core.Helpers.Forensics.Exceptions;
using VideoForensics.Core.Helpers.Forensics.KeyManagement;

namespace VideoForensics.Core.Helpers.Forensics.Tests.KeyManagement
{
    public class TpmKeyStorageProviderTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-tpm-tests-" + Guid.NewGuid().ToString("N"));
        private readonly TpmKeyStorageProvider _provider;

        public TpmKeyStorageProviderTests()
        {
            _ = Directory.CreateDirectory(_root);
            _provider = new TpmKeyStorageProvider(Path.Combine(_root, "tpm-keys.txt"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Fact]
        public void TpmKeyStorageProvider_ProviderName_IsTpm20()
        {
            Assert.Equal("TPM 2.0", _provider.ProviderName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("has space")]
        [InlineData("../escape")]
        public async Task TpmKeyStorageProvider_GetPublicKeyAsync_InvalidKeyId_Throws(string keyId)
        {
            // Key ids become CNG key names, so anything outside the allowed set is refused before the TPM is touched.
            _ = await Assert.ThrowsAnyAsync<Exception>(() => _provider.GetPublicKeyAsync(keyId));
        }

        [Fact]
        public async Task TpmKeyStorageProvider_DeleteKeyAsync_NoOfficer_IsRefusedWithoutTpm()
        {
            ForensicAnalysisException exception = await Assert.ThrowsAsync<ForensicAnalysisException>(() =>
                _provider.DeleteKeyAsync("key-1", " "));

            Assert.Contains("authorizing officer", exception.Message);
        }

        [Fact]
        public async Task TpmKeyStorageProvider_ListKeysAsync_ReadsKeyIdsFromManifest()
        {
            File.WriteAllText(Path.Combine(_root, "tpm-keys.txt"),
                "alpha\t2026-10-22T10:00:00.0000000Z" + Environment.NewLine +
                "beta\t2026-10-22T11:00:00.0000000Z" + Environment.NewLine);

            IEnumerable<string> keys = await _provider.ListKeysAsync();

            Assert.Equal(new[] { "alpha", "beta" }, keys);
        }

        [Fact]
        public async Task TpmKeyStorageProvider_ListKeysAsync_NoManifest_ReturnsEmpty()
        {
            IEnumerable<string> keys = await _provider.ListKeysAsync();

            Assert.Empty(keys);
        }

        [Fact]
        public async Task TpmKeyStorageProvider_SignAndVerify_RoundTripsWithTpmKey()
        {
            if (!_provider.IsAvailable)
            {
                Assert.Skip("TPM 2.0 key storage needs a TPM and an elevated process to create machine keys.");
            }

            string keyId = "test" + Guid.NewGuid().ToString("N")[..12];
            try
            {
                string thumbprint = await _provider.GenerateKeyPairAsync(keyId);
                byte[] data = Encoding.UTF8.GetBytes("evidence");

                string signature = await _provider.SignDataAsync(keyId, data);

                Assert.True(await _provider.VerifySignatureAsync(keyId, data, signature));
                Assert.False(await _provider.VerifySignatureAsync(keyId, Encoding.UTF8.GetBytes("tampered"), signature));
                Assert.Equal(thumbprint, (await _provider.GetKeyMetadataAsync(keyId)).CertificateThumbprint);
            }
            finally
            {
                await _provider.DeleteKeyAsync(keyId, "test-officer");
            }
        }
    }
}
