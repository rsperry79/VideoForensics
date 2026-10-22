using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Hosting.Services;

namespace VideoForensics.WebApp.Services
{
    /// <summary>Runs an account's first sync and marks it complete only when every stage succeeded.</summary>
    public interface IInitialSyncCoordinator
    {
        /// <summary>Runs discovery, config and media validation for the account. Sets the flag only on full success.</summary>
        Task RunAsync(Guid accountId, CancellationToken ct);
    }

    /// <summary>
    /// First sync for a newly linked account: discovery and config through the sync runner, then the shared validate-all
    /// pass over the account's devices as the media step. An account stays flagged incomplete until all three succeed, so
    /// an interrupted or partly failed first sync is picked up again by <see cref="InitialSyncResumer"/>.
    /// </summary>
    public class InitialSyncCoordinator : IInitialSyncCoordinator, IInitialSyncDispatcher
    {
        private readonly IAccountSyncRunner _runner;
        private readonly IProviderAccountRepository _accounts;
        private readonly IAccountDeviceRepository _accountDevices;
        private readonly BulkValidationService _validation;
        private readonly ILogger<InitialSyncCoordinator> _logger;
        private readonly ISyncActivityLog _activity;

        /// <summary>Creates the coordinator.</summary>
        public InitialSyncCoordinator(
            IAccountSyncRunner runner,
            IProviderAccountRepository accounts,
            IAccountDeviceRepository accountDevices,
            BulkValidationService validation,
            ILogger<InitialSyncCoordinator> logger,
            ISyncActivityLog activity)
        {
            _runner = runner;
            _accounts = accounts;
            _accountDevices = accountDevices;
            _validation = validation;
            _logger = logger;
            _activity = activity;
        }

        /// <inheritdoc />
        public Task RunInitialSyncAsync(Guid accountId, CancellationToken ct) => RunAsync(accountId, ct);

        /// <inheritdoc />
        public async Task RunAsync(Guid accountId, CancellationToken ct)
        {
            // Discovery, config and the event pull. The runner records its own failures on the account.
            await _runner.RunInitialAsync(accountId, ct);

            ProviderAccount? account = await _accounts.GetAsync(accountId, ct);
            if (account is null)
            {
                _logger.LogWarning("First sync finished but the provider account no longer exists");
                return;
            }

            if (account.LastErrorMessage is not null)
            {
                // Left incomplete on purpose: the flag stays false so the resume pass retries it.
                _logger.LogWarning("First sync for {ProviderName} did not finish: {Error}", account.ProviderName, account.LastErrorMessage);
                return;
            }

            // Media step: the same validate-all pass the manual action runs, scoped to this account's devices.
            IReadOnlyList<Device> devices = await _accountDevices.ListByProviderAccountAsync(accountId, ct);
            BulkValidationResult validation = await _validation.RunForDevicesAsync(devices, null, null, ct);
            _activity.Append(validation.ErrorMessage is null && validation.FailedDevices == 0
                ? $"{account.ProviderName}: validated media on {devices.Count} device(s)"
                : $"{account.ProviderName}: media validation had {validation.FailedDevices} failed device(s)");
            if (validation.ErrorMessage is not null || validation.FailedDevices > 0)
            {
                _logger.LogWarning(
                    "First sync for {ProviderName} left media validation incomplete: {FailedDevices} device(s) failed",
                    account.ProviderName, validation.FailedDevices);
                return;
            }

            account.InitialSyncCompleted = true;
            await _accounts.UpdateAsync(account, ct);
            _logger.LogInformation("First sync completed for {ProviderName}", account.ProviderName);
        }
    }
}
