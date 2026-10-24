using Microsoft.Extensions.Configuration;

using System.Reflection;

using VideoForensics.Client.Core.Licensing;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Licensing
{
    /// <summary>
    /// Tests for Syncfusion license key resolution in SyncfusionLicenseRegistrar.
    /// </summary>
    public class SyncfusionLicenseRegistrarTests
    {
        /// <summary>
        /// Verifies that RegisterIfPresent does not throw when no license file, user secret, or baked-in key is present.
        /// This is the common case in CI/dev machines without a Syncfusion license key installed.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_RegisterIfPresentNoSources_DoesNotThrow()
        {
            var registrar = new SyncfusionLicenseRegistrar();

            Exception? ex = Record.Exception(registrar.RegisterIfPresent);

            Assert.Null(ex);
        }

        /// <summary>
        /// Verifies that a user-secrets value is returned when it is the only configured source. The in-memory
        /// configuration stands in for the user-secrets layer so the test never reads a developer's real secrets file.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ConfiguredKeyFromSecretsLayerOnly_ReturnsSecretKey()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SYNCFUSION_LICENSE_KEY"] = "secret-key-123" })
                .Build();

            Assert.Equal("secret-key-123", SyncfusionLicenseRegistrar.ResolveConfiguredKey(configuration));
        }

        /// <summary>
        /// Verifies that a configuration with no SYNCFUSION_LICENSE_KEY entry resolves to null, so the baked-in key is the fallback.
        /// Uses an empty in-memory configuration; no real environment variable or secrets file is read.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ConfigWithNoKey_ReturnsNull()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection([])
                .Build();

            Assert.Null(SyncfusionLicenseRegistrar.ResolveConfiguredKey(configuration));
        }

        /// <summary>
        /// Verifies that building the configuration for an assembly without a UserSecretsId succeeds, so an
        /// optional user-secrets file being absent is not an error.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_BuildConfigurationAssemblyWithoutUserSecrets_DoesNotThrow()
        {
            Exception ex = Record.Exception(() => SyncfusionLicenseRegistrar.BuildLicenseConfiguration(typeof(SyncfusionLicenseRegistrarTests).Assembly));

            Assert.Null(ex);
        }

        /// <summary>
        /// Verifies that the configuration source returns the SYNCFUSION_LICENSE_KEY value trimmed, so user secrets or App Settings values with stray whitespace still register.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ConfiguredKeySetWithWhitespace_ReturnsTrimmedKey()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SYNCFUSION_LICENSE_KEY"] = "  test-key-123\r\n" })
                .Build();

            Assert.Equal("test-key-123", SyncfusionLicenseRegistrar.ResolveConfiguredKey(configuration));
        }

        /// <summary>
        /// Verifies that a missing or whitespace-only configured value yields null so the baked-in fallback still runs.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SyncfusionLicenseRegistrar_ConfiguredKeyBlankOrMissing_ReturnsNull(string? value)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SYNCFUSION_LICENSE_KEY"] = value })
                .Build();

            Assert.Null(SyncfusionLicenseRegistrar.ResolveConfiguredKey(configuration));
        }

        /// <summary>
        /// Verifies the file source wins over both the configured key and the baked-in key (priority 1).
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ResolveFileKeyPresent_ReturnsFileKey()
        {
            string? key = SyncfusionLicenseRegistrar.ResolveSyncfusionLicenseKey("file-key-123", "config-key-123", "baked-key-123");

            Assert.Equal("file-key-123", key);
        }

        /// <summary>
        /// Verifies the configured key wins over the baked-in key when no file is present (priority 2 over 3). This is the production path for Azure App Settings / Key Vault.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ResolveNoFileAndConfiguredKey_ReturnsConfiguredKeyOverBakedIn()
        {
            string? key = SyncfusionLicenseRegistrar.ResolveSyncfusionLicenseKey(null, "config-key-123", "baked-key-123");

            Assert.Equal("config-key-123", key);
        }

        /// <summary>
        /// Verifies a blank file or configured key is treated as absent, so the next source in the priority order is used.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SyncfusionLicenseRegistrar_ResolveBlankHigherPriorityKey_FallsThroughToBakedIn(string? blank)
        {
            Assert.Equal("baked-key-123", SyncfusionLicenseRegistrar.ResolveSyncfusionLicenseKey(blank, blank, "baked-key-123"));
        }

        /// <summary>
        /// Verifies that a configured key with surrounding whitespace is returned trimmed.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ResolveConfiguredKeyPaddedWithWhitespace_ReturnsTrimmedKey()
        {
            string? key = SyncfusionLicenseRegistrar.ResolveSyncfusionLicenseKey(null, "  config-key-123\r\n", null);

            Assert.Equal("config-key-123", key);
        }

        /// <summary>
        /// Verifies that when no source provides a key the resolver returns null, leaving the app in unlicensed dev mode.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ResolveNoSources_ReturnsNull()
        {
            Assert.Null(SyncfusionLicenseRegistrar.ResolveSyncfusionLicenseKey(null, null, null));
        }

        /// <summary>
        /// Verifies the baked-in key is read from Client.Core's own assembly metadata, so the CI-embedded key lives in Client.Core.dll.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ClientCoreAssembly_CarriesSyncfusionLicenseKeyMetadata()
        {
            bool present = typeof(SyncfusionLicenseRegistrar).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Any(a => a.Key == "SyncfusionLicenseKey");

            Assert.True(present);
        }
    }
}
