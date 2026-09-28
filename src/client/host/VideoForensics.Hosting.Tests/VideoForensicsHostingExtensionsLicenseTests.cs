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
    }
}
