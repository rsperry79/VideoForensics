using Microsoft.Extensions.Logging;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Streaming;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Ring implementation of <see cref="ILiveViewCapableProvider"/>. Establishes WebRTC live view
    /// connections to Ring cameras via the Ring SDK's Session.StartLiveView, wrapping the result
    /// in a provider-agnostic <see cref="ILiveViewConnection"/>.
    ///
    /// Follows the <see cref="RingMediaDownloadService"/> pattern: obtains the active <see cref="Session"/>
    /// via <see cref="ISessionProvider"/> rather than holding a reference directly, ensuring the
    /// provider service remains stateless and multi-account-ready.
    /// </summary>
    public class RingLiveViewProvider : ILiveViewCapableProvider
    {
        private readonly ISessionProvider _sessionProvider;
        private readonly ILogger<RingLiveViewProvider> _logger;

        public RingLiveViewProvider(ISessionProvider sessionProvider, ILogger<RingLiveViewProvider> logger)
        {
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts a live view connection with an explicit account ID.
        /// </summary>
        public async Task<ILiveViewConnection> StartLiveViewAsync(string providerDeviceId, Guid accountId, CancellationToken ct)
        {
            // Get the authenticated Session for the specific account
            var session = _sessionProvider.GetSession(accountId);
            if (session == null)
            {
                throw new InvalidOperationException($"Not authenticated with Ring for account {accountId}.");
            }

            // Parse Ring doorbot ID (should be numeric)
            if (!long.TryParse(providerDeviceId, out long doorbotId))
            {
                throw new ArgumentException($"Invalid Ring doorbot ID '{providerDeviceId}'. Expected a numeric ID.", nameof(providerDeviceId));
            }

            _logger.LogInformation("Starting Ring live view for doorbot {DoorbotId} with account {AccountId}", doorbotId, accountId);

            try
            {
                // Start the live view via the Ring SDK
                var ringSession = await session.StartLiveView(doorbotId, ct);

                // Wrap in the provider-agnostic adapter
                var connection = new RingLiveViewConnection(ringSession);

                _logger.LogInformation("Ring live view started successfully for doorbot {DoorbotId}", doorbotId);
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Ring live view for doorbot {DoorbotId}", doorbotId);
                throw;
            }
        }

        /// <summary>
        /// Starts a live view connection using the default/current authenticated session.
        /// Implements ILiveViewCapableProvider.StartLiveViewAsync.
        /// </summary>
        public async Task<ILiveViewConnection> StartLiveViewAsync(string providerDeviceId, CancellationToken ct)
        {
            // Delegate to the account-aware overload with Guid.Empty to use the default session
            return await StartLiveViewAsync(providerDeviceId, Guid.Empty, ct);
        }
    }
}
