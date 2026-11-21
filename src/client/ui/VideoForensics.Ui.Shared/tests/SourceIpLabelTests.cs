using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Loopback source addresses display as the LocalHost label; every other value passes through unchanged.</summary>
public class SourceIpLabelTests
{
    private const string LocalHostLabel = "LocalHost";

    [Theory]
    [InlineData("::1")]
    [InlineData("127.0.0.1")]
    [InlineData("::ffff:127.0.0.1")]
    public void SourceIpLabel_Format_Loopback_ReturnsLocalHostLabel(string ip)
    {
        Assert.Equal(LocalHostLabel, SourceIpLabel.Format(ip, LocalHostLabel));
    }

    [Theory]
    [InlineData("192.168.1.10")]
    [InlineData("2001:db8::1")]
    public void SourceIpLabel_Format_NonLoopback_ReturnsInputUnchanged(string ip)
    {
        Assert.Equal(ip, SourceIpLabel.Format(ip, LocalHostLabel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public void SourceIpLabel_Format_NullEmptyOrUnparseable_ReturnsInputUnchanged(string? ip)
    {
        Assert.Equal(ip, SourceIpLabel.Format(ip, LocalHostLabel));
    }
}
