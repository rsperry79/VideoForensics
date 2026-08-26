#nullable disable
using VideoForensics.Providers.Ring;

using Moq;

namespace VideoForensics.Providers.Ring.Auth.Tests
{
    public class CredentialStoreTests
    {
        [Fact]
        public void SaveAndLoadRoundTrip()
        {
            var path = Path.Combine(Path.GetTempPath(), $"ringvideos-test-auth-{Guid.NewGuid()}.json");
            var store = new CredentialStore();
            var auth = new RingCredentials
            {
                UserName = "test@example.com",
                Password = "testPassword",
                RefreshToken = "testRefresh"
            };

            try
            {
                store.Save(path, auth);
                var raw = File.ReadAllText(path);
                var loaded = store.Load(path);

                Assert.False(raw.Contains("testPassword"));
                Assert.False(raw.Contains("testRefresh"));
                Assert.Equal("test@example.com", loaded.UserName);
                Assert.Equal("testPassword", loaded.Password);
                Assert.Equal("testRefresh", loaded.RefreshToken);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void EncryptsBeforeWritingToDisk()
        {
            var path = Path.Combine(Path.GetTempPath(), $"ringvideos-test-auth-{Guid.NewGuid()}.json");
            var store = new CredentialStore();
            var auth = new RingCredentials
            {
                UserName = "user@ring.com",
                Password = "SecurePassword123!",
                RefreshToken = "refresh_abc123"
            };

            try
            {
                store.Save(path, auth);
                var raw = File.ReadAllText(path);

                Assert.False(raw.Contains("SecurePassword123!"));
                Assert.False(raw.Contains("refresh_abc123"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void Load_MissingFile_ReturnsEmptyCredentials()
        {
            var store = new CredentialStore();
            var path = Path.Combine(Path.GetTempPath(), $"ringvideos-test-auth-missing-{Guid.NewGuid()}.json");

            var loaded = store.Load(path);

            Assert.Null(loaded.UserName);
            Assert.Null(loaded.Password);
            Assert.Null(loaded.RefreshToken);
        }

        [Fact]
        public void SetCredentials_WritesRetrievableRoundTrip()
        {
            var path = Path.Combine(Path.GetTempPath(), $"ringvideos-test-auth-{Guid.NewGuid()}.json");
            var store = new CredentialStore();

            try
            {
                store.SetCredentials(path, "user@example.com", "pw", "refresh");
                var loaded = store.Load(path);

                Assert.Equal("user@example.com", loaded.UserName);
                Assert.Equal("pw", loaded.Password);
                Assert.Equal("refresh", loaded.RefreshToken);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void SanitizeClearTextPassword_MigratesAndRemovesClearTextField()
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), $"ringvideos-test-settings-{Guid.NewGuid()}.json");
            var authPath = Path.Combine(Path.GetTempPath(), $"ringvideos-test-auth-{Guid.NewGuid()}.json");
            var store = new CredentialStore();

            try
            {
                File.WriteAllText(settingsPath, "{\"Password\":\"clear-text-secret\",\"Other\":1}");

                var migrated = store.SanitizeClearTextPassword(settingsPath, authPath);

                Assert.True(migrated);
                Assert.False(File.ReadAllText(settingsPath).Contains("clear-text-secret"));
                Assert.Equal("clear-text-secret", store.Load(authPath).Password);
            }
            finally
            {
                if (File.Exists(settingsPath))
                    File.Delete(settingsPath);
                if (File.Exists(authPath))
                    File.Delete(authPath);
            }
        }

        /// <summary>
        /// ICredentialStore exists specifically so consumers like RingVideoService can be constructed
        /// with a fake in tests instead of touching disk - this confirms the interface actually
        /// satisfies that: a mock can stand in wherever ICredentialStore is expected.
        /// </summary>
        [Fact]
        public void ICredentialStore_IsMockable()
        {
            var mock = new Mock<ICredentialStore>();
            mock.Setup(s => s.Load(It.IsAny<string>())).Returns(new RingCredentials { RefreshToken = "fake-token" });

            ICredentialStore store = mock.Object;
            var result = store.Load("irrelevant-path");

            Assert.Equal("fake-token", result.RefreshToken);
            mock.Verify(s => s.Load("irrelevant-path"), Times.Once);
        }
    }
}
