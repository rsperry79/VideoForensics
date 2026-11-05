using Xunit;
using VideoForensics.Client.Platform.Windows.KeyManagement;

namespace VideoForensics.Client.Platform.Windows.Tests.KeyManagement
{
    public class DataProtectionKeyWrappingTests
    {
        [Fact]
        public void Describe_TpmCertificateUsed_NamesTheTpm()
        {
            Assert.Equal("TPM-held certificate", DataProtectionKeyWrapping.Describe(tpmCertificateUsed: true, tpmProbeAvailable: true, elevated: true));
        }

        [Fact]
        public void Describe_FellBackToDpapi_StatesTheProbeAndElevationSoTheLogShowsWhy()
        {
            string text = DataProtectionKeyWrapping.Describe(tpmCertificateUsed: false, tpmProbeAvailable: false, elevated: false);

            Assert.Contains("DPAPI", text);
            Assert.Contains("TPM available: False", text);
            Assert.Contains("elevated: False", text);
        }
    }
}
