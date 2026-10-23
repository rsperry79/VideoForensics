namespace VideoForensics.Hosting.Tests
{
    public class CertificateRenewalPolicyTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void CertificateRenewalPolicy_IsRenewalDue_NoCertificate_IsDue()
        {
            Assert.True(CertificateRenewalPolicy.IsRenewalDue(null, Now));
        }

        [Fact]
        public void CertificateRenewalPolicy_IsRenewalDue_MoreThan30DaysLeft_IsNotDue()
        {
            Assert.False(CertificateRenewalPolicy.IsRenewalDue(Now.AddDays(31), Now));
        }

        [Fact]
        public void CertificateRenewalPolicy_IsRenewalDue_Exactly30DaysLeft_IsDue()
        {
            Assert.True(CertificateRenewalPolicy.IsRenewalDue(Now.AddDays(30), Now));
        }

        [Fact]
        public void CertificateRenewalPolicy_IsRenewalDue_Expired_IsDue()
        {
            Assert.True(CertificateRenewalPolicy.IsRenewalDue(Now.AddDays(-1), Now));
        }

        [Fact]
        public void CertificateRenewalPolicy_FindZoneForHost_ChoosesLongestMatchingZone()
        {
            CloudflareZone[] zones = new[]
            {
                new CloudflareZone("parent", "example.com", "acct"),
                new CloudflareZone("child", "lab.example.com", "acct"),
            };

            CloudflareZone? zone = CertificateRenewalPolicy.FindZoneForHost(zones, "video.lab.example.com");

            Assert.Equal("child", zone?.Id);
        }

        [Fact]
        public void CertificateRenewalPolicy_FindZoneForHost_ApexHostnameMatchesZone()
        {
            CloudflareZone[] zones = new[] { new CloudflareZone("parent", "Example.com", "acct") };

            Assert.Equal("parent", CertificateRenewalPolicy.FindZoneForHost(zones, "example.com.")?.Id);
        }

        [Fact]
        public void CertificateRenewalPolicy_FindZoneForHost_NoCoveringZone_ReturnsNull()
        {
            CloudflareZone[] zones = new[] { new CloudflareZone("parent", "example.com", "acct") };

            Assert.Null(CertificateRenewalPolicy.FindZoneForHost(zones, "notexample.com"));
        }

        [Fact]
        public void CertificateRenewalPolicy_FindZoneForHost_SuffixWithoutDotDoesNotMatch()
        {
            CloudflareZone[] zones = new[] { new CloudflareZone("parent", "ample.com", "acct") };

            Assert.Null(CertificateRenewalPolicy.FindZoneForHost(zones, "example.com"));
        }
    }
}
