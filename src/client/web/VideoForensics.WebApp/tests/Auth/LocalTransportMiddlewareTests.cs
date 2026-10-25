using System.Net;

using Microsoft.AspNetCore.Http;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Auth;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Auth;

/// <summary>The middleware redirects plain-http LAN requests and leaves secure and loopback requests alone.</summary>
public class LocalTransportMiddlewareTests
{
    private static readonly IPAddress Lan = IPAddress.Parse("192.168.1.20");
    private const string PublicUrl = "https://forensics.example.com";

    private static (DefaultHttpContext Context, bool NextCalled) Run(
        ICloudflaredTunnelService tunnels,
        LocalTransportOptions options,
        bool https,
        IPAddress remote,
        string host = "192.168.1.5:5000",
        string path = "/account/sign-in",
        string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = https ? "https" : "http";
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Connection.RemoteIpAddress = remote;

        bool nextCalled = false;
        var middleware = new LocalTransportMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<LocalTransportMiddleware>.Instance);
        middleware.InvokeAsync(context, tunnels, options).GetAwaiter().GetResult();
        return (context, nextCalled);
    }

    private static ICloudflaredTunnelService TunnelRunning()
    {
        var tunnels = new Mock<ICloudflaredTunnelService>();
        _ = tunnels.Setup(t => t.GetState()).Returns(new TunnelState(TunnelKind.Named, TunnelStatus.Running, PublicUrl, null, []));
        return tunnels.Object;
    }

    private static ICloudflaredTunnelService TunnelStopped()
    {
        var tunnels = new Mock<ICloudflaredTunnelService>();
        _ = tunnels.Setup(t => t.GetState()).Returns(new TunnelState(TunnelKind.None, TunnelStatus.Stopped, null, null, []));
        return tunnels.Object;
    }

    private static LocalTransportOptions HttpsReady() => new(HttpsPort: 5443, HttpsCertificateReady: () => true);

    private static LocalTransportOptions NoHttps() => new(HttpsPort: null, HttpsCertificateReady: () => false);

    [Fact]
    public void LanHttp_TunnelRunning_RedirectsToTunnelKeepingPathAndQuery()
    {
        (DefaultHttpContext context, bool nextCalled) = Run(TunnelRunning(), HttpsReady(), https: false, remote: Lan, query: "?returnUrl=%2Fcases");

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal(PublicUrl + "/account/sign-in?returnUrl=%2Fcases", context.Response.Headers.Location.ToString());
        Assert.False(nextCalled);
    }

    [Fact]
    public void LanHttp_NoTunnel_HttpsAvailable_RedirectsToHttpsOnTheSamePath()
    {
        (DefaultHttpContext context, bool nextCalled) = Run(TunnelStopped(), HttpsReady(), https: false, remote: Lan);

        Assert.Equal("https://192.168.1.5:5443/account/sign-in", context.Response.Headers.Location.ToString());
        Assert.False(nextCalled);
    }

    [Fact]
    public void LanHttp_NoTunnelNoHttps_ServesAndFlagsPasskeyUnavailable()
    {
        (DefaultHttpContext context, bool nextCalled) = Run(TunnelStopped(), NoHttps(), https: false, remote: Lan);

        Assert.True(nextCalled);
        Assert.Equal(true, context.Items[LocalTransportMiddleware.PasskeyUnavailableItemKey]);
    }

    [Fact]
    public void Https_IsServedWithoutRedirect()
    {
        (DefaultHttpContext context, bool nextCalled) = Run(TunnelRunning(), HttpsReady(), https: true, remote: Lan);

        Assert.True(nextCalled);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public void Loopback_TunnelConnector_IsServedSoTheTunnelCannotLoop()
    {
        (DefaultHttpContext context, bool nextCalled) = Run(TunnelRunning(), HttpsReady(), https: false, remote: IPAddress.Loopback, host: "forensics.example.com");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task LanHttp_ApiPost_IsNotRedirected()
    {
        // The tunnel is started by a POST to /api. A redirect would drop the body, so API calls always pass through.
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("192.168.1.5:5000");
        context.Request.Path = "/api/remote-access/start";
        context.Connection.RemoteIpAddress = Lan;

        bool nextCalled = false;
        var middleware = new LocalTransportMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<LocalTransportMiddleware>.Instance);
        await middleware.InvokeAsync(context, TunnelStopped(), HttpsReady());

        Assert.True(nextCalled);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }
}
