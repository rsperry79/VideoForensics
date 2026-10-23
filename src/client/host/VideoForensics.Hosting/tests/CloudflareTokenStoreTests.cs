namespace VideoForensics.Client.Hosting.Tests
{
    public class CloudflareTokenStoreTests
    {
        private const string ApiTokenPlain = "cf-account-api-token";
        private const string TunnelTokenPlain = "cf-permanent-tunnel-token";

        private readonly Dictionary<string, string> _rows = [];
        private readonly Mock<IAppSettingRepository> _settings = new();
        private readonly Mock<ICredentialEncryptionProvider> _encryption = new();
        private readonly CloudflareTokenStore _store;

        public CloudflareTokenStoreTests()
        {
            _ = _settings.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) => _rows.TryGetValue(key, out string? value) ? value : null);
            _ = _settings.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string key, string value, CancellationToken _) => _rows[key] = value)
                .Returns(Task.CompletedTask);
            _ = _settings.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string key, CancellationToken ct) => _ = _rows.Remove(key))
                .Returns(Task.CompletedTask);

            // Reversible stand-in for the real cipher. Reversing keeps the plaintext out of the stored value, so the
            // "no plaintext stored" assertion means something.
            _ = _encryption.Setup(e => e.EncryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string plain, CancellationToken _) => "enc:" + new string(plain.Reverse().ToArray()));
            _ = _encryption.Setup(e => e.DecryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string encrypted, CancellationToken _) => new string(encrypted["enc:".Length..].Reverse().ToArray()));

            _store = new CloudflareTokenStore(_settings.Object, _encryption.Object);
        }

        [Fact]
        public async Task SetApiTokenAsync_ThenGet_RoundTripsWithoutStoringPlaintext()
        {
            await _store.SetApiTokenAsync(ApiTokenPlain, CancellationToken.None);

            Assert.DoesNotContain(_rows.Values, v => v.Contains(ApiTokenPlain, StringComparison.Ordinal));
            Assert.Equal(ApiTokenPlain, await _store.GetDecryptedApiTokenAsync(CancellationToken.None));
        }

        [Fact]
        public async Task SetTunnelTokenAsync_ThenGet_RoundTripsAndStaysSeparateFromApiToken()
        {
            await _store.SetApiTokenAsync(ApiTokenPlain, CancellationToken.None);
            await _store.SetTunnelTokenAsync(TunnelTokenPlain, CancellationToken.None);

            Assert.Equal(TunnelTokenPlain, await _store.GetDecryptedTunnelTokenAsync(CancellationToken.None));
            Assert.Equal(ApiTokenPlain, await _store.GetDecryptedApiTokenAsync(CancellationToken.None));
            Assert.Equal(2, _rows.Count);
        }

        [Fact]
        public async Task ClearAsync_RemovesBothTokens()
        {
            await _store.SetApiTokenAsync(ApiTokenPlain, CancellationToken.None);
            await _store.SetTunnelTokenAsync(TunnelTokenPlain, CancellationToken.None);

            await _store.ClearAsync(CancellationToken.None);

            Assert.Null(await _store.GetDecryptedApiTokenAsync(CancellationToken.None));
            Assert.Null(await _store.GetDecryptedTunnelTokenAsync(CancellationToken.None));
            Assert.Empty(_rows);
        }

        [Fact]
        public async Task GetDecryptedApiTokenAsync_NothingStored_ReturnsNull()
        {
            Assert.Null(await _store.GetDecryptedApiTokenAsync(CancellationToken.None));
            _encryption.Verify(e => e.DecryptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SetApiTokenAsync_EmptyToken_ThrowsAndStoresNothing(string token)
        {
            _ = await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.SetApiTokenAsync(token, CancellationToken.None));

            Assert.Empty(_rows);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task SetTunnelTokenAsync_EmptyToken_ThrowsAndStoresNothing(string token)
        {
            _ = await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.SetTunnelTokenAsync(token, CancellationToken.None));

            Assert.Empty(_rows);
        }
    }
}
