using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Configuration;

using VideoForensics.Client.WebApp.Infrastructure;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Infrastructure
{
    public class ServerPortTests
    {
        [Fact]
        public void ServerAddressResolver_IsPortAvailable_PortHeldByListener_ReturnsFalse()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            try
            {
                Assert.False(ServerAddressResolver.IsPortAvailable(port));
            }
            finally
            {
                listener.Stop();
            }

            Assert.True(ServerAddressResolver.IsPortAvailable(port));
        }

        [Fact]
        public void ServerAddressResolver_ResolveListenEndpoints_SavedHttpPort_ReplacesUrlsPortOnly()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["urls"] = "http://localhost:5162;https://localhost:7004",
                    ["ServerHttpPort"] = "6001",
                })
                .Build();

            var endpoints = ServerAddressResolver.ResolveListenEndpoints(configuration);

            Assert.Equal(6001, endpoints.Single(e => e.Scheme == "http").Port);
            Assert.Equal(7004, endpoints.Single(e => e.Scheme == "https").Port);
        }

        [Fact]
        public void ServerAddressResolver_ResolveListenEndpoints_SavedHttpsPortWithoutHttpsUrl_AddsHttpsListener()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["urls"] = "http://localhost:5162",
                    ["ServerHttpsPort"] = "7443",
                })
                .Build();

            var endpoints = ServerAddressResolver.ResolveListenEndpoints(configuration);

            Assert.Equal(5162, endpoints.Single(e => e.Scheme == "http").Port);
            Assert.Equal(7443, endpoints.Single(e => e.Scheme == "https").Port);
        }

        [Fact]
        public void ServerAddressResolver_ResolveListenEndpoints_SavedHttpsPortWithNoUrls_KeepsDefaultHttp()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ServerHttpsPort"] = "7443" })
                .Build();

            var endpoints = ServerAddressResolver.ResolveListenEndpoints(configuration);

            Assert.Equal(5162, endpoints.Single(e => e.Scheme == "http").Port);
            Assert.Equal(7443, endpoints.Single(e => e.Scheme == "https").Port);
        }

        [Fact]
        public void ServerAddressResolver_ResolveListenEndpoints_NoSavedPort_KeepsUrlsPorts()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["urls"] = "http://localhost:5162;https://localhost:7004",
                    ["ServerHttpPort"] = "0",
                })
                .Build();

            var endpoints = ServerAddressResolver.ResolveListenEndpoints(configuration);

            Assert.Equal(5162, endpoints.Single(e => e.Scheme == "http").Port);
            Assert.Equal(7004, endpoints.Single(e => e.Scheme == "https").Port);
        }
    }
}
