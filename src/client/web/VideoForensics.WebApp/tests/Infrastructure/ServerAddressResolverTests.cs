using Microsoft.Extensions.Configuration;
using Xunit;
using VideoForensics.WebApp.Infrastructure;

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
        public void ServerAddressResolver_ResolveListenEndpoints_HttpsAndHttp_ReturnsBothWithSchemes()
        {
            var result = ServerAddressResolver.ResolveListenEndpoints(Build("https://localhost:7004;http://localhost:5162"));

            Assert.Equal(new[] { new ListenEndpoint("https", 7004), new ListenEndpoint("http", 5162) }, result);
        }

        [Fact]
        public void ServerAddressResolver_ResolveListenEndpoints_NoConfig_DefaultsToHttp5162()
        {
            var result = ServerAddressResolver.ResolveListenEndpoints(Build(null));

            Assert.Equal(new[] { new ListenEndpoint("http", 5162) }, result);
        }

        [Fact]
        public void ServerAddressResolver_ResolveAllowedOrigins_HttpsAndHttp_ReturnsBothLocalhostOrigins()
        {
            var result = ServerAddressResolver.ResolveAllowedOrigins(Build("https://localhost:7004;http://localhost:5162"));

            Assert.Equal(new HashSet<string> { "https://localhost:7004", "http://localhost:5162" }, result.ToHashSet());
        }

        [Fact]
        public void ServerAddressResolver_ResolveAllowedOrigins_NoConfig_DefaultsToHttp5162Origin()
        {
            var result = ServerAddressResolver.ResolveAllowedOrigins(Build(null));

            Assert.Equal(new HashSet<string> { "http://localhost:5162" }, result.ToHashSet());
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
    }
}
