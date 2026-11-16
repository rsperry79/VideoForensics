using Microsoft.Extensions.Logging;

using MimeKit;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting
{
    /// <summary>
    /// Email is the baseline urgent-notification channel (plan §5.6) - the one channel that
    /// reaches the owner regardless of whether any client app is open, for a self-hosted app with
    /// no cloud backend. Operator-scoped events go to that operator's own address, and only when
    /// the operator's email preference allows the event type. System-wide events (no operator)
    /// go to the global notification recipient.
    /// </summary>
    public class EmailNotificationProvider : INotificationProvider
    {
        private readonly IForensicsConfiguration _config;
        private readonly ISmtpMailSender _mailSender;
        private readonly IOperatorRepository _operators;
        private readonly IOperatorNotificationPreferenceRepository _preferences;
        private readonly ILogger<EmailNotificationProvider> _logger;
        private readonly ICloudflaredTunnelService? _tunnels;

        /// <summary>Creates the provider. <paramref name="tunnels"/> supplies the public address for links in the body; without it, no link is added.</summary>
        public EmailNotificationProvider(
            IForensicsConfiguration config,
            ISmtpMailSender mailSender,
            IOperatorRepository operators,
            IOperatorNotificationPreferenceRepository preferences,
            ILogger<EmailNotificationProvider> logger,
            ICloudflaredTunnelService? tunnels = null)
        {
            _config = config;
            _mailSender = mailSender;
            _operators = operators;
            _preferences = preferences;
            _logger = logger;
            _tunnels = tunnels;
        }

        public string Name => "Email";

        public Task<bool> IsEnabledAsync(CancellationToken ct)
        {
            bool configured = _config.EnableEmailNotifications
                && !string.IsNullOrWhiteSpace(_config.SmtpHost)
                && !string.IsNullOrWhiteSpace(_config.SmtpFromAddress)
                && !string.IsNullOrWhiteSpace(_config.NotificationRecipientEmail);
            return Task.FromResult(configured);
        }

        /// <summary>
        /// Routes the event. An event with an operator is sent to that operator's address only when their preference has email
        /// enabled and lists the event type; a missing preference row, a missing address, or a disallowed type is a logged skip.
        /// An event with no operator is sent to the global recipient regardless of preferences.
        /// </summary>
        public async Task SendAsync(NotificationEvent notificationEvent, CancellationToken ct)
        {
            if (notificationEvent.OperatorId is null)
            {
                await SendToAsync(_config.NotificationRecipientEmail, notificationEvent, ct);
                return;
            }

            Operator? operatorEntity = await _operators.GetAsync(notificationEvent.OperatorId.Value, ct);
            if (operatorEntity is null)
            {
                _logger.LogInformation("Skipping email for {EventType} - operator not found", notificationEvent.EventType);
                return;
            }

            OperatorNotificationPreference? preferences = await _preferences.GetAsync(notificationEvent.OperatorId.Value, ct);
            if (preferences is null || !preferences.EmailEnabled || !preferences.EmailEventTypeSet.Contains(notificationEvent.EventType))
            {
                _logger.LogInformation(
                    "Skipping email for {EventType} - email not enabled for this event for operator {Username}",
                    notificationEvent.EventType,
                    operatorEntity.Username);
                return;
            }

            if (string.IsNullOrWhiteSpace(operatorEntity.Email))
            {
                _logger.LogInformation(
                    "Skipping email for {EventType} - operator {Username} has no email address",
                    notificationEvent.EventType,
                    operatorEntity.Username);
                return;
            }

            await SendToAsync(operatorEntity.Email, notificationEvent, ct);
        }

        /// <summary>
        /// The tunnel's public address with no trailing slash, or null when the tunnel is not running. Email cannot use a relative
        /// link, so links in the body are built from this address.
        /// </summary>
        private string? TunnelBaseUrl()
        {
            TunnelState? state = _tunnels?.GetState();
            return state is { Status: TunnelStatus.Running, PublicUrl: not null } ? state.PublicUrl.TrimEnd('/') : null;
        }

        /// <summary>Used by the "Send Test Email" button on the Notifications settings screen - a real send through the configured SMTP settings, not a dry run, since that's the only way to actually confirm the settings work.</summary>
        public async Task SendTestEmailAsync(CancellationToken ct)
        {
            MimeMessage message = BuildMessage(
                _config.NotificationRecipientEmail,
                "[VideoForensics] Test notification",
                "This is a test email from VideoForensics' notification settings. If you received this, email notifications are configured correctly.");
            await _mailSender.SendAsync(message, ct);
        }

        private async Task SendToAsync(string to, NotificationEvent notificationEvent, CancellationToken ct)
        {
            MimeMessage message = BuildMessage(
                to,
                $"[VideoForensics] {notificationEvent.EventType}",
                BuildBody(notificationEvent, TunnelBaseUrl()));
            await _mailSender.SendAsync(message, ct);
        }

        private MimeMessage BuildMessage(string to, string subject, string body)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_config.SmtpFromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };
            return message;
        }

        /// <summary>
        /// Builds the plain-text body. The link is added only when the event has an <see cref="NotificationEvent.ActionPath"/> and
        /// there is a tunnel address to make it absolute.
        /// </summary>
        internal static string BuildBody(NotificationEvent evt, string? tunnelBaseUrl)
        {
            var lines = new List<string>
            {
                $"Event: {evt.EventType}",
                $"Time (UTC): {evt.TimestampUtc:u}"
            };

            if (evt.OperatorId is not null)
            {
                lines.Add($"Operator: {evt.OperatorId}");
            }

            if (evt.PairedDeviceId is not null)
            {
                lines.Add($"Device: {evt.PairedDeviceId}");
            }

            if (evt.SourceIp is not null)
            {
                lines.Add($"Source IP: {evt.SourceIp}");
            }

            if (!string.IsNullOrEmpty(evt.Details))
            {
                lines.Add($"Details: {evt.Details}");
            }

            if (evt.ActionPath is not null && tunnelBaseUrl is not null)
            {
                lines.Add($"Open: {tunnelBaseUrl}{evt.ActionPath}");
            }

            lines.Add("");
            lines.Add("This is an urgent security event from your VideoForensics server. Check the Security Audit Log for full context.");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
