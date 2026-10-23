using Microsoft.Extensions.Configuration;

using Xunit;

namespace VideoForensics.WebApp.Tests.Infrastructure
{
    public class ServerAddressResolverTests
    {
        private static IConfiguration Build(string? urls)
        {
            var values = new Dictionary<string, string?>();
            if (urls is not null)
            {
                values["urls"] = urls;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        [Fact]
        public void ServerAddressResolver_ResolveAllowedOrigins_ConfiguredCloudflareOrigin_UsesPublicHost()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["urls"] = "https://localhost:443",
                ["WebAuthn:Origins"] = "https://video.example.trycloudflare.com"
            }).Build();

            Assert.Contains("https://video.example.trycloudflare.com", ServerAddressResolver.ResolveAllowedOrigins(configuration));
        }

        [Fact]
        public void ServerAddressResolver_ResolveAllowedOrigins_HttpsAndHttp_ReturnsBothLocalhostOrigins()
        {
            IReadOnlySet<string> result = ServerAddressResolver.ResolveAllowedOrigins(Build("https://localhost:7004;http://localhost:5162"));

            Assert.Equal(["https://localhost:7004", "http://localhost:5162"], result.ToHashSet());
        }

        [Fact]
        public void ServerAddressResolver_ResolveAllowedOrigins_NoConfig_DefaultsToHttp5162Origin()
        {
            IReadOnlySet<string> result = ServerAddressResolver.ResolveAllowedOrigins(Build(null));

            Assert.Equal(["http://localhost:5162"], result.ToHashSet());
        }

        [Fact]
        public void ServerAddressResolver_ResolveHttpPort_HttpsFirst_ReturnsHttpPort()
        {
            Assert.Equal(5162, ServerAddressResolver.ResolveHttpPort(Build("https://localhost:7004;http://localhost:5162")));
        }

        [Fact]
        public void ServerAddressResolver_ResolveHttpPort_NoHttpUrl_DefaultsTo5162()
        {
            Assert.Equal(5162, ServerAddressResolver.ResolveHttpPort(Build("https://localhost:7004")));
        }

        [Fact]
        public void ServerAddressResolver_ResolveListenEndpoints_HttpsAndHttp_ReturnsBothWithSchemes()
        {
            IReadOnlyList<ListenEndpoint> result = ServerAddressResolver.ResolveListenEndpoints(Build("https://localhost:7004;http://localhost:5162"));

            Assert.Equal([new ListenEndpoint("https", 7004), new ListenEndpoint("http", 5162)], result);
        }

        [Fact]
        public void ServerAddressResolver_ResolveListenEndpoints_NoConfig_DefaultsToHttp5162()
        {
            IReadOnlyList<ListenEndpoint> result = ServerAddressResolver.ResolveListenEndpoints(Build(null));

            Assert.Equal([new ListenEndpoint("http", 5162)], result);
        }

        [Fact]
        public void ServerAddressResolver_ResolveRpId_UsesConfiguredTunnelHost()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebAuthn:RpId"] = "video.example.trycloudflare.com"
            }).Build();

            Assert.Equal("video.example.trycloudflare.com", ServerAddressResolver.ResolveRpId(configuration));
        }

        [Fact]
        public void ServerAddressResolver_ResolveRpId_NoExplicitRpIdUsesConfiguredInternetHost()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder().Build();

            Assert.Equal("video.example.com", ServerAddressResolver.ResolveRpId(configuration, "https://video.example.com"));
        }

        [Fact]
        public void ServerAddressResolver_ResolveRpId_ExplicitRpIdWinsOverConfiguredInternetHost()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebAuthn:RpId"] = "explicit.example.com"
            }).Build();

            Assert.Equal("explicit.example.com", ServerAddressResolver.ResolveRpId(configuration, "https://video.example.com"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a url")]
        [InlineData("http://video.example.com")]
        public void ServerAddressResolver_ResolveRpId_UnusableInternetUrlFallsBackToLocalhost(string? internetUrl)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder().Build();

            Assert.Equal("localhost", ServerAddressResolver.ResolveRpId(configuration, internetUrl));
        }
    }
}
