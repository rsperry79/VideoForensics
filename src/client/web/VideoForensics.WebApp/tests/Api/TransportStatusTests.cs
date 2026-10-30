using Microsoft.AspNetCore.Http;

using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api;

/// <summary>The transport status tells the sign-in page whether passkeys work on the address it was loaded from.</summary>
public class TransportStatusTests
{
    [Fact]
    public void NoUnavailableFlag_ReportsPasskeysAvailable()
    {
        var context = new DefaultHttpContext();

        TransportStatus status = AuthMethodEndpoints.GetTransportStatus(context);

        Assert.True(status.PasskeyAvailable);
    }

    [Fact]
    public void UnavailableFlagFromMiddleware_ReportsPasskeysUnavailable()
    {
        var context = new DefaultHttpContext();
        context.Items[LocalTransportMiddleware.PasskeyUnavailableItemKey] = true;

        TransportStatus status = AuthMethodEndpoints.GetTransportStatus(context);

        Assert.False(status.PasskeyAvailable);
    }

    [Fact]
    public void TunnelAddressGiven_ReportsItForPasskeyRedirect()
    {
        // A passkey attempt from localhost is sent to the tunnel, so the page needs the tunnel's address.
        var context = new DefaultHttpContext();

        TransportStatus status = AuthMethodEndpoints.GetTransportStatus(context, "https://video.example.com");

        Assert.Equal("https://video.example.com", status.TunnelUrl);
    }

    [Fact]
    public void TunnelRunning_ReportsItsAddressWithoutTrailingSlash()
    {
        var state = new TunnelState(TunnelKind.Named, TunnelStatus.Running, "https://video.example.com/", null, []);

        Assert.Equal("https://video.example.com", AuthMethodEndpoints.TunnelUrlOf(state));
    }

    [Fact]
    public void TunnelStopped_ReportsNoAddress()
    {
        var state = new TunnelState(TunnelKind.None, TunnelStatus.Stopped, null, null, []);

        Assert.Null(AuthMethodEndpoints.TunnelUrlOf(state));
    }
}
