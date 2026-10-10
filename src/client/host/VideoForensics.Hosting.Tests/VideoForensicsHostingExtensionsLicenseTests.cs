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
    }
}
