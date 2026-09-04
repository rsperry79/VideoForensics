using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using VideoForensics.Client.Common;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Email is the baseline urgent-notification channel (plan §5.6) - the one channel that
    /// reaches the owner regardless of whether any client app is open, for a self-hosted app with
    /// no cloud backend. Uses MailKit rather than the obsolete <c>System.Net.Mail.SmtpClient</c>.
    /// </summary>
    public class EmailNotificationProvider : INotificationProvider
    {
        private readonly IForensicsConfiguration _config;
        private readonly ISmtpPasswordStore _passwordStore;
        private readonly ILogger<EmailNotificationProvider> _logger;

        public EmailNotificationProvider(IForensicsConfiguration config, ISmtpPasswordStore passwordStore, ILogger<EmailNotificationProvider> logger)
        {
            _config = config;
            _passwordStore = passwordStore;
            _logger = logger;
        }

        public string Name => "Email";

        public Task<bool> IsEnabledAsync(CancellationToken ct)
        {
            var configured = _config.EnableEmailNotifications
                && !string.IsNullOrWhiteSpace(_config.SmtpHost)
                && !string.IsNullOrWhiteSpace(_config.SmtpFromAddress)
                && !string.IsNullOrWhiteSpace(_config.NotificationRecipientEmail);
            return Task.FromResult(configured);
        }

        public async Task SendAsync(NotificationEvent notificationEvent, CancellationToken ct)
        {
            var message = BuildMessage(
                _config.NotificationRecipientEmail,
                $"[VideoForensics] {notificationEvent.EventType}",
                BuildBody(notificationEvent));
            await SendMessageAsync(message, ct);
        }

        /// <summary>Used by the "Send Test Email" button on the Notifications settings screen - a real send through the configured SMTP settings, not a dry run, since that's the only way to actually confirm the settings work.</summary>
        public async Task SendTestEmailAsync(CancellationToken ct)
        {
            var message = BuildMessage(
                _config.NotificationRecipientEmail,
                "[VideoForensics] Test notification",
                "This is a test email from VideoForensics' notification settings. If you received this, email notifications are configured correctly.");
            await SendMessageAsync(message, ct);
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

        private async Task SendMessageAsync(MimeMessage message, CancellationToken ct)
        {
            using var client = new SmtpClient();
            var socketOptions = _config.SmtpUseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_config.SmtpHost, _config.SmtpPort, socketOptions, ct);

            if (!string.IsNullOrEmpty(_config.SmtpUsername))
            {
                var password = await _passwordStore.GetDecryptedAsync(ct) ?? "";
                await client.AuthenticateAsync(_config.SmtpUsername, password, ct);
            }

            try
            {
                await client.SendAsync(message, ct);
            }
            finally
            {
                await client.DisconnectAsync(true, ct);
            }
        }

        private static string BuildBody(NotificationEvent evt)
        {
            var lines = new List<string>
            {
                $"Event: {evt.EventType}",
                $"Time (UTC): {evt.TimestampUtc:u}"
            };

            if (evt.OperatorId is not null) lines.Add($"Operator: {evt.OperatorId}");
            if (evt.PairedDeviceId is not null) lines.Add($"Device: {evt.PairedDeviceId}");
            if (evt.SourceIp is not null) lines.Add($"Source IP: {evt.SourceIp}");
            if (!string.IsNullOrEmpty(evt.Details)) lines.Add($"Details: {evt.Details}");

            lines.Add("");
            lines.Add("This is an urgent security event from your VideoForensics server. Check the Security Audit Log for full context.");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
