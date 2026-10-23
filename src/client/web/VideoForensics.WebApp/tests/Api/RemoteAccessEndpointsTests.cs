using Microsoft.Extensions.Configuration;

using Moq;

using VideoForensics.Hosting;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    public class RemoteAccessEndpointsTests
    {
        [Theory]
        [InlineData("video")]
        [InlineData("cam-1")]
        [InlineData("A1")]
        public void RemoteAccessEndpoints_IsSafeLabel_AcceptsValidLabels(string label)
        {
            Assert.True(RemoteAccessEndpoints.IsSafeLabel(label));
        }

        [Theory]
        [InlineData("")]
        [InlineData("-video")]
        [InlineData("video-")]
        [InlineData("vid.eo")]
        [InlineData("vid_eo")]
        [InlineData("vidéo")]
        public void RemoteAccessEndpoints_IsSafeLabel_RejectsInvalidLabels(string label)
        {
            Assert.False(RemoteAccessEndpoints.IsSafeLabel(label));
        }

        [Fact]
        public void RemoteAccessEndpoints_IsSafeLabel_RejectsLongerThan63Characters()
        {
            Assert.False(RemoteAccessEndpoints.IsSafeLabel(new string('a', 64)));
            Assert.True(RemoteAccessEndpoints.IsSafeLabel(new string('a', 63)));
        }

        [Theory]
        [InlineData("example.com")]
        [InlineData("sub.example.co.uk")]
        public void RemoteAccessEndpoints_IsSafeDomain_AcceptsDottedDomains(string domain)
        {
            Assert.True(RemoteAccessEndpoints.IsSafeDomain(domain));
        }

        [Theory]
        [InlineData("localhost")]
        [InlineData("example.com/path")]
        [InlineData("example.com:8443")]
        [InlineData("user@example.com")]
        [InlineData("")]
        public void RemoteAccessEndpoints_IsSafeDomain_RejectsNonDomains(string domain)
        {
            Assert.False(RemoteAccessEndpoints.IsSafeDomain(domain));
        }

        [Fact]
        public void RemoteAccessEndpoints_BuildFqdn_LowersCaseAndTrimsTrailingDot()
        {
            Assert.Equal("video.example.com", RemoteAccessEndpoints.BuildFqdn(" Video ", "Example.COM."));
        }

        [Fact]
        public async Task RemoteAccessEndpoints_LookUpZoneAsync_DomainReturnedByToken_ReturnsZoneWithAccount()
        {
            var tokens = new Mock<ICloudflareTokenStore>();
            _ = tokens.Setup(t => t.GetDecryptedApiTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("api-token");
            var cloudflare = new Mock<ICloudflareApiClient>();
            _ = cloudflare.Setup(c => c.ValidateTokenAndListZonesAsync("api-token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CloudflareZoneResult(true, new[] { new CloudflareZone("zone-1", "example.com", "acct-1") }, null));

            RemoteAccessEndpoints.ZoneLookup lookup = await RemoteAccessEndpoints.LookUpZoneAsync(tokens.Object, cloudflare.Object, "EXAMPLE.com", CancellationToken.None);

            Assert.Null(lookup.Error);
            Assert.Equal("zone-1", lookup.Zone?.Id);
            Assert.Equal("acct-1", lookup.Zone?.AccountId);
        }

        [Fact]
        public async Task RemoteAccessEndpoints_LookUpZoneAsync_DomainNotReturnedByToken_IsRejected()
        {
            var tokens = new Mock<ICloudflareTokenStore>();
            _ = tokens.Setup(t => t.GetDecryptedApiTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("api-token");
            var cloudflare = new Mock<ICloudflareApiClient>();
            _ = cloudflare.Setup(c => c.ValidateTokenAndListZonesAsync("api-token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CloudflareZoneResult(true, new[] { new CloudflareZone("zone-1", "example.com", "acct-1") }, null));

            RemoteAccessEndpoints.ZoneLookup lookup = await RemoteAccessEndpoints.LookUpZoneAsync(tokens.Object, cloudflare.Object, "other.net", CancellationToken.None);

            Assert.Null(lookup.Zone);
            Assert.NotNull(lookup.Error);
        }

        [Fact]
        public async Task RemoteAccessEndpoints_LookUpZoneAsync_NoStoredToken_AsksForToken()
        {
            var tokens = new Mock<ICloudflareTokenStore>();
            _ = tokens.Setup(t => t.GetDecryptedApiTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
            var cloudflare = new Mock<ICloudflareApiClient>();

            RemoteAccessEndpoints.ZoneLookup lookup = await RemoteAccessEndpoints.LookUpZoneAsync(tokens.Object, cloudflare.Object, "example.com", CancellationToken.None);

            Assert.Equal("Configure a Cloudflare API token first.", lookup.Error);
            cloudflare.Verify(c => c.ValidateTokenAndListZonesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void RemoteAccessEndpoints_PasskeyOrigins_IncludesSavedInternetOriginOnce()
        {
            IConfigurationRoot configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();

            string[] origins = RemoteAccessEndpoints.PasskeyOrigins(configuration, "https://video.example.com");

            Assert.Contains("https://video.example.com", origins);
            Assert.Single(origins, "https://video.example.com");
        }

        [Fact]
        public void RemoteAccessEndpoints_PasskeyOrigins_NoInternetAddress_ListsOnlyLocalOrigins()
        {
            IConfigurationRoot configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();

            string[] origins = RemoteAccessEndpoints.PasskeyOrigins(configuration, null);

            Assert.DoesNotContain(origins, o => o.Contains("video.example.com", StringComparison.Ordinal));
            Assert.Contains("http://localhost:5162", origins);
        }
    }
}
