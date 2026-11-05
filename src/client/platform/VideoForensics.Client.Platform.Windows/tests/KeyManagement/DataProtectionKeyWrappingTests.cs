using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Xunit;
using VideoForensics.Client.Platform.Windows.KeyManagement;

namespace VideoForensics.Client.Platform.Windows.Tests.KeyManagement
{
    public class DataProtectionKeyWrappingTests
    {
        private static readonly TimeSpan[] Delays = [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

        private static X509Certificate2 SelfSigned()
        {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        }

        [Fact]
        public void ResolveTpmCertificate_TpmRecoversOnRetry_ReturnsItAfterWaiting()
        {
            using X509Certificate2 cert = SelfSigned();
            var results = new Queue<X509Certificate2?>([null, null, cert]);
            var waits = new List<TimeSpan>();

            X509Certificate2? resolved = DataProtectionKeyWrapping.ResolveTpmCertificate(
                () => results.Dequeue(), () => true, waits.Add, Delays);

            Assert.Same(cert, resolved);
            Assert.Equal(Delays[..2], waits);
        }

        [Fact]
        public void ResolveTpmCertificate_TpmKeyExistsAndEveryAttemptFails_RefusesToStart()
        {
            var waits = new List<TimeSpan>();

            Assert.Throws<InvalidOperationException>(() => DataProtectionKeyWrapping.ResolveTpmCertificate(
                () => null, () => true, waits.Add, Delays));
            Assert.Equal(Delays, waits);
        }

        [Fact]
        public void ResolveTpmCertificate_NoTpmKeyAndEveryAttemptFails_AllowsDpapi()
        {
            X509Certificate2? resolved = DataProtectionKeyWrapping.ResolveTpmCertificate(
                () => null, () => false, _ => { }, Delays);

            Assert.Null(resolved);
        }

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
