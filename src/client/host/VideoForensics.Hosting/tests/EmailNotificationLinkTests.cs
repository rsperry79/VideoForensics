using VideoForensics.Core.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>Email cannot open a relative link, so the email body gets the tunnel's absolute address when there is one.</summary>
    public class EmailNotificationLinkTests
    {
        private static NotificationEvent Event(string? actionPath) =>
            new("DeviceOffline", DateTime.UtcNow, null, null, null, "Front Door went offline", ActionPath: actionPath);

        [Fact]
        public void BuildBody_ActionPathAndTunnel_AddsAbsoluteTunnelLink()
        {
            string body = EmailNotificationProvider.BuildBody(Event("/accounts/1/sync"), "https://video.example.com");

            Assert.Contains("Open: https://video.example.com/accounts/1/sync", body);
        }

        [Fact]
        public void BuildBody_ActionPathWithoutTunnel_OmitsLink()
        {
            string body = EmailNotificationProvider.BuildBody(Event("/accounts/1/sync"), null);

            Assert.DoesNotContain("Open:", body);
        }

        [Fact]
        public void BuildBody_NoActionPath_OmitsLink()
        {
            string body = EmailNotificationProvider.BuildBody(Event(null), "https://video.example.com");

            Assert.DoesNotContain("Open:", body);
        }
    }
}
