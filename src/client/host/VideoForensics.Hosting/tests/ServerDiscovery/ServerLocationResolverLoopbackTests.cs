using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting.ServerDiscovery;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.ServerDiscovery
{
    public class ServerLocationResolverLoopbackTests
    {
        private static readonly Func<CancellationToken, Task<Uri?>> NoMdns = _ => Task.FromResult<Uri?>(null);

        [Fact]
        public async Task Resolve_ReturnsLoopback_WhenLocalServerAnswers()
        {
            // Same-device MAUI must reach the server on loopback: the server only applies its local password
            // exemption to direct loopback requests, and a LAN address would be treated as remote.
            var probe = new FakeLoopbackProbe(running: true);
            var resolver = new ServerLocationResolver(new FakeSettingsStore(null), NullLogger<ServerLocationResolver>.Instance, probe, NoMdns);

            Uri uri = await resolver.ResolveServerAddressAsync(CancellationToken.None);

            Assert.Equal(ServerLocationResolver.LoopbackServerUri, uri);
            Assert.Equal(1, probe.Calls);
        }

        [Fact]
        public async Task Resolve_FallsBackToCachedUrl_WhenLoopbackDown()
        {
            var probe = new FakeLoopbackProbe(running: false);
            var resolver = new ServerLocationResolver(
                new FakeSettingsStore("https://vf.example.com/"),
                NullLogger<ServerLocationResolver>.Instance,
                probe,
                NoMdns);

            Uri uri = await resolver.ResolveServerAddressAsync(CancellationToken.None);

            Assert.Equal(new Uri("https://vf.example.com/"), uri);
        }

        [Fact]
        public async Task Resolve_TreatsProbeException_AsNotRunning()
        {
            var probe = new FakeLoopbackProbe(throwOnProbe: true);
            var resolver = new ServerLocationResolver(
                new FakeSettingsStore("https://vf.example.com/"),
                NullLogger<ServerLocationResolver>.Instance,
                probe,
                NoMdns);

            Uri uri = await resolver.ResolveServerAddressAsync(CancellationToken.None);

            Assert.Equal(new Uri("https://vf.example.com/"), uri);
        }

        private sealed class FakeLoopbackProbe : ILoopbackServerProbe
        {
            private readonly bool _running;
            private readonly bool _throwOnProbe;

            public FakeLoopbackProbe(bool running = false, bool throwOnProbe = false)
            {
                _running = running;
                _throwOnProbe = throwOnProbe;
            }

            public int Calls { get; private set; }

            public Task<bool> IsServerRunningAsync(Uri baseUri, CancellationToken ct)
            {
                Calls++;
                if (_throwOnProbe)
                {
                    throw new InvalidOperationException("probe failed");
                }

                return Task.FromResult(_running);
            }
        }

        private sealed class FakeSettingsStore : IServerLocationSettingsStore
        {
            private readonly string? _cached;

            public FakeSettingsStore(string? cached)
            {
                _cached = cached;
            }

            public string? GetCachedInternetServerUrl() => _cached;

            public void SetCachedInternetServerUrl(string url)
            {
            }
        }
    }
}
