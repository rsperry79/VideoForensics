using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Re-runs the first sync for every active account whose first sync never finished.</summary>
    public class InitialSyncResumer
    {
        private readonly IProviderAccountRepository _accounts;
        private readonly IInitialSyncCoordinator _coordinator;
        private readonly ILogger<InitialSyncResumer> _logger;

        /// <summary>Creates the resumer.</summary>
        public InitialSyncResumer(
            IProviderAccountRepository accounts,
            IInitialSyncCoordinator coordinator,
            ILogger<InitialSyncResumer> logger)
        {
            _accounts = accounts;
            _coordinator = coordinator;
            _logger = logger;
        }

        /// <summary>
        /// Runs each incomplete account's first sync in turn. One account failing is logged and does not stop the rest.
        /// </summary>
        public async Task ResumeIncompleteAsync(CancellationToken ct)
        {
            IReadOnlyList<ProviderAccount> active = await _accounts.ListActiveAsync(ct);
            foreach (ProviderAccount account in active.Where(a => !a.InitialSyncCompleted))
            {
                try
                {
                    _logger.LogInformation("Resuming first sync for {ProviderName}", account.ProviderName);
                    await _coordinator.RunAsync(account.Id, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Resuming first sync failed for {ProviderName}", account.ProviderName);
                }
            }
        }
    }
}
