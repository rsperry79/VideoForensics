using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;
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
        private const string RingProviderName = "Ring";

        private readonly ISessionProvider _sessionProvider;
        private readonly IProviderAuthService _authService;
        private readonly ILogger<RingLiveViewProvider> _logger;

        /// <inheritdoc />
        public string ProviderName => RingProviderName;

        public RingLiveViewProvider(
            ISessionProvider sessionProvider,
            [FromKeyedServices("Ring")] IProviderAuthService authService,
            ILogger<RingLiveViewProvider> logger)
        {
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts a live view connection for a device of the given Ring account. The account's session is held in memory,
        /// so after a restart it is restored from the saved credentials first.
        /// </summary>
        public async Task<ILiveViewConnection> StartLiveViewAsync(string providerDeviceId, Guid providerAccountId, CancellationToken ct)
        {
            Session? session = _sessionProvider.GetSession(providerAccountId);
            if (session == null && await _authService.RestoreFromSavedCredentialsAsync(providerAccountId, ct))
            {
                session = _sessionProvider.GetSession(providerAccountId);
            }

            if (session == null)
            {
                throw new InvalidOperationException("Not authenticated with Ring for this account. Sign in again from the account's page.");
            }

            // Parse Ring doorbot ID (should be numeric)
            if (!long.TryParse(providerDeviceId, out long doorbotId))
            {
                throw new ArgumentException($"Invalid Ring doorbot ID '{providerDeviceId}'. Expected a numeric ID.", nameof(providerDeviceId));
            }

            _logger.LogDebug("Starting Ring live view for doorbot {DoorbotId} with {ProviderName} account", doorbotId, RingProviderName);

            try
            {
                // Start the live view via the Ring SDK
                RingLiveViewSession ringSession = await session.StartLiveView(doorbotId, ct);

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
    }
}
