using Microsoft.Extensions.Logging;

using MimeKit;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>Operator-scoped events go to that operator's address only when their email preference allows it; system-wide events keep the global recipient.</summary>
    public class EmailNotificationRoutingTests
    {
        private const string GlobalRecipient = "global@example.com";
        private const string OperatorAddress = "alice@example.com";

        private readonly Guid _operatorId = Guid.NewGuid();
        private readonly Mock<IForensicsConfiguration> _config = new();
        private readonly Mock<ISmtpMailSender> _sender = new();
        private readonly Mock<IOperatorRepository> _operators = new();
        private readonly Mock<IOperatorNotificationPreferenceRepository> _preferences = new();

        public EmailNotificationRoutingTests()
        {
            _ = _config.SetupGet(c => c.SmtpFromAddress).Returns("vf@example.com");
            _ = _config.SetupGet(c => c.NotificationRecipientEmail).Returns(GlobalRecipient);

            _ = _operators.Setup(o => o.GetAsync(_operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Operator
                {
                    Username = "alice",
                    DisplayName = "Alice",
                    FirstName = "Alice",
                    LastName = "Anderson",
                    Email = OperatorAddress,
                });
        }

        private static NotificationEvent OperatorEvent(Guid? operatorId, string eventType = "DeviceOffline")
        {
            return new(eventType, DateTime.UtcNow, operatorId, null, null, "Front Door went offline");
        }

        private static OperatorNotificationPreference Prefs(bool emailEnabled, string eventTypes)
        {
            return new OperatorNotificationPreference { OperatorId = Guid.Empty, EmailEnabled = emailEnabled, EmailEventTypes = eventTypes };
        }

        private EmailNotificationProvider BuildProvider()
        {
            return new EmailNotificationProvider(
                _config.Object,
                _sender.Object,
                _operators.Object,
                _preferences.Object,
                new Mock<ILogger<EmailNotificationProvider>>().Object);
        }

        [Fact]
        public async Task SendAsync_OperatorEmailDisabled_DoesNotSend()
        {
            _ = _preferences.Setup(p => p.GetAsync(_operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Prefs(emailEnabled: false, "DeviceOffline"));

            await BuildProvider().SendAsync(OperatorEvent(_operatorId), CancellationToken.None);

            _sender.Verify(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SendAsync_OperatorEnabledAndEventListed_SendsOnceToOperatorAddress()
        {
            _ = _preferences.Setup(p => p.GetAsync(_operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Prefs(emailEnabled: true, "DeviceOffline,AccountRemoved"));

            await BuildProvider().SendAsync(OperatorEvent(_operatorId), CancellationToken.None);

            _sender.Verify(
                s => s.SendAsync(It.Is<MimeMessage>(m => m.To.Mailboxes.Single().Address == OperatorAddress), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendAsync_OperatorEnabledButEventNotListed_DoesNotSend()
        {
            _ = _preferences.Setup(p => p.GetAsync(_operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Prefs(emailEnabled: true, "AccountRemoved"));

            await BuildProvider().SendAsync(OperatorEvent(_operatorId), CancellationToken.None);

            _sender.Verify(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SendAsync_OperatorHasNoPreferenceRow_DoesNotSend()
        {
            _ = _preferences.Setup(p => p.GetAsync(_operatorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((OperatorNotificationPreference?)null);

            await BuildProvider().SendAsync(OperatorEvent(_operatorId), CancellationToken.None);

            _sender.Verify(s => s.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SendAsync_NoOperatorId_SendsToGlobalRecipientWithoutCheckingPreferences()
        {
            await BuildProvider().SendAsync(OperatorEvent(null), CancellationToken.None);

            _sender.Verify(
                s => s.SendAsync(It.Is<MimeMessage>(m => m.To.Mailboxes.Single().Address == GlobalRecipient), It.IsAny<CancellationToken>()),
                Times.Once);
            _preferences.Verify(p => p.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
