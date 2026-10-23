namespace VideoForensics.Client.Hosting.Tests
{
    public class LetsEncryptCertificateStoreTests
    {
        private readonly Dictionary<string, string> _rows = [];
        private readonly Mock<IAppSettingRepository> _settings = new();
        private readonly Mock<ICredentialEncryptionProvider> _encryption = new();
        private readonly LetsEncryptCertificateStore _store;

        public LetsEncryptCertificateStoreTests()
        {
            _ = _settings.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken ct) => _rows.TryGetValue(key, out string? value) ? value : null);
            _ = _settings.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string key, string value, CancellationToken ct) => _rows[key] = value)
                .Returns(Task.CompletedTask);

            // Reversed-string stand-in for the cipher, so stored values never contain the plaintext.
            _ = _encryption.Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string plain, CancellationToken ct) => "enc:" + new string(plain.Reverse().ToArray()));
            _ = _encryption.Setup(e => e.DecryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string encrypted, CancellationToken ct) => new string(encrypted["enc:".Length..].Reverse().ToArray()));

            _store = new LetsEncryptCertificateStore(_settings.Object, _encryption.Object);
        }

        [Fact]
        public async Task LetsEncryptCertificateStore_AccountKey_RoundTripsWithoutPlaintextAtRest()
        {
            const string pem = "-----BEGIN EC PRIVATE KEY-----test-----END EC PRIVATE KEY-----";

            await _store.SaveAccountAsync(new AcmeAccountState(pem), CancellationToken.None);

            Assert.DoesNotContain(_rows.Values, v => v.Contains("BEGIN EC PRIVATE KEY", StringComparison.Ordinal));
            Assert.Equal(pem, (await _store.GetAccountAsync(CancellationToken.None))?.AccountKeyPem);
        }

        [Fact]
        public async Task LetsEncryptCertificateStore_Certificate_RoundTripsWithExpiry()
        {
            var expiry = new DateTimeOffset(2027, 1, 19, 12, 30, 0, TimeSpan.Zero);
            await _store.SaveCertificateAsync(new IssuedCertificate("pem-bundle", expiry, string.Empty), CancellationToken.None);

            IssuedCertificate? loaded = await _store.GetCertificateAsync(CancellationToken.None);

            Assert.Equal("pem-bundle", loaded?.PemBundle);
            Assert.Equal(expiry, loaded?.NotAfter);
        }

        [Fact]
        public async Task LetsEncryptCertificateStore_NothingStored_ReturnsNull()
        {
            Assert.Null(await _store.GetAccountAsync(CancellationToken.None));
            Assert.Null(await _store.GetCertificateAsync(CancellationToken.None));
            Assert.Null(await _store.GetContactEmailAsync(CancellationToken.None));
        }

        [Fact]
        public async Task LetsEncryptCertificateStore_CertificateWithUnreadableExpiry_ReturnsNull()
        {
            _rows["Acme.EncryptedCertificate"] = "enc:" + new string("pem".Reverse().ToArray());
            _rows["Acme.CertificateNotAfter"] = "not-a-date";

            Assert.Null(await _store.GetCertificateAsync(CancellationToken.None));
        }

        [Fact]
        public async Task LetsEncryptCertificateStore_ContactEmail_IsStoredTrimmedAndUnencrypted()
        {
            await _store.SetContactEmailAsync("  ops@example.com ", CancellationToken.None);

            Assert.Equal("ops@example.com", await _store.GetContactEmailAsync(CancellationToken.None));
            _encryption.Verify(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
