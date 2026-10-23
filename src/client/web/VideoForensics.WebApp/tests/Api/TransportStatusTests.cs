using Microsoft.AspNetCore.Http;

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
}
