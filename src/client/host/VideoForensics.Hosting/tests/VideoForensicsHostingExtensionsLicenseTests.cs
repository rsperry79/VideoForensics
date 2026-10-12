using Microsoft.Extensions.Configuration;
using Xunit;

namespace VideoForensics.Hosting.Tests
{
    /// <summary>
    /// Tests for Syncfusion license registration in VideoForensicsHostingExtensions.
    /// </summary>
    public class VideoForensicsHostingExtensionsLicenseTests
    {
        /// <summary>
        /// Verifies that RegisterSyncfusionLicenseIfPresent does not throw when the license key file is missing.
        /// This is the common case in CI/dev machines without a Syncfusion license key installed.
        /// </summary>
        [Fact]
        public void RegisterSyncfusionLicenseIfPresent_KeyFileMissing_DoesNotThrow()
        {
            // Act & Assert: calling the method should not throw even though the license file doesn't exist
            var ex = Record.Exception(() => VideoForensicsHostingExtensions.RegisterSyncfusionLicenseIfPresent());

            Assert.Null(ex);
        }

        /// <summary>
        /// Verifies the env var source returns the key with surrounding whitespace trimmed, so a key pasted with a trailing newline still registers.
        /// </summary>
        [Fact]
        public void GetSyncfusionLicenseKeyFromEnvironment_KeySet_ReturnsTrimmedKey()
        {
            const string variable = "SYNCFUSION_LICENSE_KEY";
            string? previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, "  test-key-123\r\n");

                Assert.Equal("test-key-123", VideoForensicsHostingExtensions.GetSyncfusionLicenseKeyFromEnvironment());
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
        public void GetSyncfusionLicenseKeyFromEnvironment_KeyBlankOrUnset_ReturnsNull(string? value)
        {
            const string variable = "SYNCFUSION_LICENSE_KEY";
            string? previous = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, value);

                Assert.Null(VideoForensicsHostingExtensions.GetSyncfusionLicenseKeyFromEnvironment());
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
        public void GetSyncfusionLicenseKeyFromConfiguration_KeySet_ReturnsTrimmedKey()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SYNCFUSION_LICENSE_KEY"] = "  test-key-123\r\n" })
                .Build();

            Assert.Equal("test-key-123", VideoForensicsHostingExtensions.GetSyncfusionLicenseKeyFromConfiguration(configuration));
        }

        /// <summary>
        /// Verifies that a missing or whitespace-only configuration value yields null so the baked-in fallback still runs.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetSyncfusionLicenseKeyFromConfiguration_KeyBlankOrMissing_ReturnsNull(string? value)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["SYNCFUSION_LICENSE_KEY"] = value })
                .Build();

            Assert.Null(VideoForensicsHostingExtensions.GetSyncfusionLicenseKeyFromConfiguration(configuration));
        }

        /// <summary>
        /// Verifies the file source wins over both the configured key and the baked-in key (priority 1).
        /// </summary>
        [Fact]
        public void ResolveSyncfusionLicenseKey_FileKeyPresent_ReturnsFileKey()
        {
            string? key = VideoForensicsHostingExtensions.ResolveSyncfusionLicenseKey("file-key-123", "config-key-123", "baked-key-123");

            Assert.Equal("file-key-123", key);
        }

        /// <summary>
        /// Verifies the configured key wins over the baked-in key when no file is present (priority 2 over 3). This is the production path for Azure App Settings / Key Vault.
        /// </summary>
        [Fact]
        public void ResolveSyncfusionLicenseKey_NoFileAndConfiguredKey_ReturnsConfiguredKeyOverBakedIn()
        {
            string? key = VideoForensicsHostingExtensions.ResolveSyncfusionLicenseKey(null, "config-key-123", "baked-key-123");

            Assert.Equal("config-key-123", key);
        }

        /// <summary>
        /// Verifies a blank file or configured key is treated as absent, so the next source in the priority order is used.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ResolveSyncfusionLicenseKey_BlankHigherPriorityKey_FallsThroughToBakedIn(string? blank)
        {
            Assert.Equal("baked-key-123", VideoForensicsHostingExtensions.ResolveSyncfusionLicenseKey(blank, blank, "baked-key-123"));
        }

        /// <summary>
        /// Verifies that a configured key with surrounding whitespace is returned trimmed.
        /// </summary>
        [Fact]
        public void ResolveSyncfusionLicenseKey_ConfiguredKeyPaddedWithWhitespace_ReturnsTrimmedKey()
        {
            string? key = VideoForensicsHostingExtensions.ResolveSyncfusionLicenseKey(null, "  config-key-123\r\n", null);

            Assert.Equal("config-key-123", key);
        }

        /// <summary>
        /// Verifies that when no source provides a key the resolver returns null, leaving the app in unlicensed dev mode.
        /// </summary>
        [Fact]
        public void ResolveSyncfusionLicenseKey_NoSources_ReturnsNull()
        {
            Assert.Null(VideoForensicsHostingExtensions.ResolveSyncfusionLicenseKey(null, null, null));
        }
    }
}
