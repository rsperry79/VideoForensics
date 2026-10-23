using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Layout;

namespace VideoForensics.Client.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards the provider-account writes that the Accounts and account-wizard pages make in-process. Reads stay open
    /// because Collect and Device Configuration need them for every operator. Background sync (the Record methods) is
    /// not an operator action and passes through.
    /// </summary>
    public sealed class GuardedProviderAccountRepository(IProviderAccountRepository inner, ICircuitOperatorGate gate) : IProviderAccountRepository
    {
        public Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct)
        {
            return inner.GetAsync(accountId, ct);
        }

        public Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return inner.GetByUserIdAsync(userId, ct);
        }

        public Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct)
        {
            return inner.GetByUserAndProviderAsync(userId, providerName, ct);
        }

        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct)
        {
            return inner.ListAsync(ct);
        }

        public Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct)
        {
            return inner.ListActiveAsync(ct);
        }

        public Task<IReadOnlyList<ProviderAccount>> ListRemovedAsync(CancellationToken ct)
        {
            return inner.ListRemovedAsync(ct);
        }

        public Task RetireAsync(Guid accountId, DateTime removedUtc, CancellationToken ct, string? stepUpToken = null)
        {
            Require(AccessPolicy.SuperAdminLocal);
            return inner.RetireAsync(accountId, removedUtc, ct, stepUpToken);
        }

        public Task AddAsync(ProviderAccount account, CancellationToken ct)
        {
            Require(AccessPolicy.SuperAdminLocal);
            return inner.AddAsync(account, ct);
        }

        public Task UpdateAsync(ProviderAccount account, CancellationToken ct)
        {
            Require(AccessPolicy.SuperAdminLocal);
            return inner.UpdateAsync(account, ct);
        }

        public Task DeleteAsync(Guid accountId, CancellationToken ct, string? stepUpToken = null)
        {
            Require(AccessPolicy.SuperAdminLocal);
            return inner.DeleteAsync(accountId, ct, stepUpToken);
        }

        public Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken)
        {
            return inner.RecordErrorAsync(providerAccountId, errorMessage, cancellationToken);
        }

        public Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken)
        {
            return inner.RecordSuccessAsync(providerAccountId, cancellationToken);
        }

        private void Require(AccessPolicy policy)
        {
            if (!gate.Allows(policy))
            {
                throw new UnauthorizedAccessException(gate.Denial(policy));
            }
        }
    }
}
