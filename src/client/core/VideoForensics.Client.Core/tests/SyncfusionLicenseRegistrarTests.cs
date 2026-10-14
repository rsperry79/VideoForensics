using System.Reflection;

using Microsoft.Extensions.Configuration;

using VideoForensics.Client.Core.Licensing;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    /// <summary>
    /// Tests for Syncfusion license key resolution in SyncfusionLicenseRegistrar.
    /// </summary>
    public class SyncfusionLicenseRegistrarTests
    {
        /// <summary>
        /// Verifies that RegisterIfPresent does not throw when the license key file is missing.
        /// This is the common case in CI/dev machines without a Syncfusion license key installed.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_RegisterIfPresentKeyFileMissing_DoesNotThrow()
        {
            var registrar = new SyncfusionLicenseRegistrar();

            var ex = Record.Exception(() => registrar.RegisterIfPresent());

            Assert.Null(ex);
        }

        /// <summary>
        /// Verifies that the configuration overload rejects a null configuration instead of silently skipping registration.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_RegisterIfPresentNullConfiguration_ThrowsArgumentNullException()
        {
            var registrar = new SyncfusionLicenseRegistrar();

            Assert.Throws<ArgumentNullException>(() => registrar.RegisterIfPresent(null!));
        }

        /// <summary>
        /// Verifies the env var source returns the key with surrounding whitespace trimmed, so a key pasted with a trailing newline still registers.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_EnvironmentKeySetWithWhitespace_ReturnsTrimmedKey()
        {
            const string variable = "SYNCFUSION_LICENSE_KEY";
            string? previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, "  test-key-123\r\n");

                Assert.Equal("test-key-123", SyncfusionLicenseRegistrar.GetSyncfusionLicenseKeyFromEnvironment());
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }

        /// <summary>
        /// Verifies that an unset or whitespace-only env var yields null so the baked-in fallback still runs.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SyncfusionLicenseRegistrar_EnvironmentKeyBlankOrUnset_ReturnsNull(string? value)
        {
            const string variable = "SYNCFUSION_LICENSE_KEY";
            string? previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, value);

                Assert.Null(SyncfusionLicenseRegistrar.GetSyncfusionLicenseKeyFromEnvironment());
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }

        /// <summary>
        /// Verifies the configuration source returns the SYNCFUSION_LICENSE_KEY value trimmed, so user secrets or App Settings values with stray whitespace still register.
        /// </summary>
        [Fact]
        public void SyncfusionLicenseRegistrar_ConfigurationKeySetWithWhitespace_ReturnsTrimmedKey()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SYNCFUSION_LICENSE_KEY"] = "  test-key-123\r\n" })
                .Build();

            Assert.Equal("test-key-123", SyncfusionLicenseRegistrar.GetSyncfusionLicenseKeyFromConfiguration(configuration));
        }

        /// <summary>
        /// Verifies that a missing or whitespace-only configuration value yields null so the baked-in fallback still runs.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SyncfusionLicenseRegistrar_ConfigurationKeyBlankOrMissing_ReturnsNull(string? value)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SYNCFUSION_LICENSE_KEY"] = value })
                .Build();

            Assert.Null(SyncfusionLicenseRegistrar.GetSyncfusionLicenseKeyFromConfiguration(configuration));
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
