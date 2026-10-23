using VideoForensics.Client.WebApp.Auth;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Auth;

/// <summary>Plain-http LAN requests are redirected to a secure address when one exists, and told passkeys are unavailable when none does.</summary>
public class LocalTransportPolicyTests
{
    [Fact]
    public void Https_IsServedAsIs()
    {
        Assert.Equal(LocalTransportAction.Serve, LocalTransportPolicy.Decide(isSecureOrigin: true, isLoopback: false, tunnelRunning: true, httpsAvailable: true));
    }

    [Fact]
    public void Loopback_IsServedEvenOverHttp()
    {
        Assert.Equal(LocalTransportAction.Serve, LocalTransportPolicy.Decide(isSecureOrigin: false, isLoopback: true, tunnelRunning: false, httpsAvailable: false));
    }

    [Fact]
    public void LanHttp_WithTunnelRunning_RedirectsToTunnel()
    {
        Assert.Equal(LocalTransportAction.RedirectToTunnel, LocalTransportPolicy.Decide(isSecureOrigin: false, isLoopback: false, tunnelRunning: true, httpsAvailable: true));
    }

    [Fact]
    public void LanHttp_WithoutTunnel_RedirectsToHttpsWhenAvailable()
    {
        Assert.Equal(LocalTransportAction.RedirectToHttps, LocalTransportPolicy.Decide(isSecureOrigin: false, isLoopback: false, tunnelRunning: false, httpsAvailable: true));
    }

    [Fact]
    public void LanHttp_WithNoSecureAddress_ReportsPasskeyUnavailable()
    {
        Assert.Equal(LocalTransportAction.PasskeyUnavailable, LocalTransportPolicy.Decide(isSecureOrigin: false, isLoopback: false, tunnelRunning: false, httpsAvailable: false));
    }
}
