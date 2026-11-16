using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;

namespace VideoForensics.Client.Hosting
{
    /// <summary>Default <see cref="ISmtpMailSender"/>: sends through the SMTP settings in <see cref="IForensicsConfiguration"/> using MailKit.</summary>
    public class MailKitSmtpMailSender : ISmtpMailSender
    {
        private readonly IForensicsConfiguration _config;
        private readonly ISmtpPasswordStore _passwordStore;

        /// <summary>Creates the sender.</summary>
        public MailKitSmtpMailSender(IForensicsConfiguration config, ISmtpPasswordStore passwordStore)
        {
            _config = config;
            _passwordStore = passwordStore;
        }

        /// <inheritdoc />
        public async Task SendAsync(MimeMessage message, CancellationToken ct)
        {
            using var client = new SmtpClient();
            SecureSocketOptions socketOptions = _config.SmtpUseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_config.SmtpHost, _config.SmtpPort, socketOptions, ct);

            if (!string.IsNullOrEmpty(_config.SmtpUsername))
            {
                string password = await _passwordStore.GetDecryptedAsync(ct) ?? "";
                await client.AuthenticateAsync(_config.SmtpUsername, password, ct);
            }

            try
            {
                _ = await client.SendAsync(message, ct);
            }
            finally
            {
                await client.DisconnectAsync(true, ct);
            }
        }
    }
}
