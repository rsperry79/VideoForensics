using MimeKit;

namespace VideoForensics.Client.Hosting.Contracts
{
    /// <summary>
    /// Delivers one composed email over the configured SMTP server. This seam keeps the routing in
    /// <see cref="EmailNotificationProvider"/> testable without a real server.
    /// </summary>
    public interface ISmtpMailSender
    {
        /// <summary>Connects, authenticates when a username is configured, sends the message, and disconnects.</summary>
        /// <param name="message">The composed message to send.</param>
        /// <param name="ct">Cancellation token.</param>
        Task SendAsync(MimeMessage message, CancellationToken ct);
    }
}
