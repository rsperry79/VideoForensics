using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class LetsEncryptCertificateManagerTests
    {
        private static IssuedCertificate CreatePemBundle(out X509Certificate2 original)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=video.example.com", key, HashAlgorithmName.SHA256);
            original = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));
            string pem = original.ExportCertificatePem() + key.ExportPkcs8PrivateKeyPem();
            return new IssuedCertificate(pem, original.NotAfter, string.Empty);
        }

        [Fact]
        public void LetsEncryptCertificateManager_ToX509_PemBundleYieldsCertificateWithPrivateKey()
        {
            IssuedCertificate bundle = CreatePemBundle(out X509Certificate2 original);

            using X509Certificate2 converted = LetsEncryptCertificateManager.ToX509(bundle);

            Assert.True(converted.HasPrivateKey);
            Assert.Equal("CN=video.example.com", converted.Subject);
            Assert.Equal(original.Thumbprint, converted.Thumbprint);
        }

        [Fact]
        public void CertificateHolder_Replace_MakesCertificateCurrent()
        {
            IssuedCertificate bundle = CreatePemBundle(out _);
            var holder = new CertificateHolder();
            Assert.Null(holder.Current);

            holder.Replace(LetsEncryptCertificateManager.ToX509(bundle));

            Assert.NotNull(holder.Current);
            Assert.Equal("CN=video.example.com", holder.Current!.Subject);
        }

        [Fact]
        public void CertificateHolder_Clear_RemovesCertificate()
        {
            IssuedCertificate bundle = CreatePemBundle(out _);
            var holder = new CertificateHolder();
            holder.Replace(LetsEncryptCertificateManager.ToX509(bundle));

            holder.Clear();

            Assert.Null(holder.Current);
        }

        [Fact]
        public void LetsEncryptCertificateManager_PublicChainPem_OmitsPrivateKey()
        {
            IssuedCertificate bundle = CreatePemBundle(out _);

            string chain = LetsEncryptCertificateManager.PublicChainPem(bundle.PemBundle);

            Assert.Contains("-----BEGIN CERTIFICATE-----", chain, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE KEY", chain, StringComparison.Ordinal);
        }
    }
}
