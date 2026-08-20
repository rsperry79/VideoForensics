#nullable disable
using Ring.Api;

using Moq;

namespace Ring.Api.Auth.Tests
{
    [TestClass]
    public class CredentialStoreTests
    {
        [TestMethod]
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

                Assert.IsFalse(raw.Contains("testPassword"));
                Assert.IsFalse(raw.Contains("testRefresh"));
                Assert.AreEqual("test@example.com", loaded.UserName);
                Assert.AreEqual("testPassword", loaded.Password);
                Assert.AreEqual("testRefresh", loaded.RefreshToken);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
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

                Assert.IsFalse(raw.Contains("SecurePassword123!"));
                Assert.IsFalse(raw.Contains("refresh_abc123"));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_MissingFile_ReturnsEmptyCredentials()
        {
            var store = new CredentialStore();
            var path = Path.Combine(Path.GetTempPath(), $"ringvideos-test-auth-missing-{Guid.NewGuid()}.json");

            var loaded = store.Load(path);

            Assert.IsNull(loaded.UserName);
            Assert.IsNull(loaded.Password);
            Assert.IsNull(loaded.RefreshToken);
        }

        [TestMethod]
        public void SetCredentials_WritesRetrievableRoundTrip()
        {
            var path = Path.Combine(Path.GetTempPath(), $"ringvideos-test-auth-{Guid.NewGuid()}.json");
            var store = new CredentialStore();

            try
            {
                store.SetCredentials(path, "user@example.com", "pw", "refresh");
                var loaded = store.Load(path);

                Assert.AreEqual("user@example.com", loaded.UserName);
                Assert.AreEqual("pw", loaded.Password);
                Assert.AreEqual("refresh", loaded.RefreshToken);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [TestMethod]
        public void SanitizeClearTextPassword_MigratesAndRemovesClearTextField()
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), $"ringvideos-test-settings-{Guid.NewGuid()}.json");
            var authPath = Path.Combine(Path.GetTempPath(), $"ringvideos-test-auth-{Guid.NewGuid()}.json");
            var store = new CredentialStore();

            try
            {
                File.WriteAllText(settingsPath, "{\"Password\":\"clear-text-secret\",\"Other\":1}");

                var migrated = store.SanitizeClearTextPassword(settingsPath, authPath);

                Assert.IsTrue(migrated);
                Assert.IsFalse(File.ReadAllText(settingsPath).Contains("clear-text-secret"));
                Assert.AreEqual("clear-text-secret", store.Load(authPath).Password);
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
        [TestMethod]
        public void ICredentialStore_IsMockable()
        {
            var mock = new Mock<ICredentialStore>();
            mock.Setup(s => s.Load(It.IsAny<string>())).Returns(new RingCredentials { RefreshToken = "fake-token" });

            ICredentialStore store = mock.Object;
            var result = store.Load("irrelevant-path");

            Assert.AreEqual("fake-token", result.RefreshToken);
            mock.Verify(s => s.Load("irrelevant-path"), Times.Once);
        }
    }
}
