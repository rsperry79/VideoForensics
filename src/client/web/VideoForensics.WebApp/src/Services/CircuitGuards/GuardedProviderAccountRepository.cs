using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards the provider-account writes that the Accounts and account-wizard pages make in-process. Reads stay open
    /// because Collect and Device Configuration need them for every operator. Background sync (the Record methods) is
    /// not an operator action and passes through.
    /// </summary>
    public sealed class GuardedProviderAccountRepository(IProviderAccountRepository inner, ICircuitOperatorGate gate) : IProviderAccountRepository
    {
        public Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct) => inner.GetAsync(accountId, ct);

        public Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct) => inner.GetByUserIdAsync(userId, ct);

        public Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct) =>
            inner.GetByUserAndProviderAsync(userId, providerName, ct);

        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct) => inner.ListAsync(ct);

        public Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct) => inner.ListActiveAsync(ct);

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

        public Task DeleteAsync(Guid accountId, CancellationToken ct)
        {
            Require(AccessPolicy.SuperAdminLocal);
            return inner.DeleteAsync(accountId, ct);
        }

        public Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken) =>
            inner.RecordErrorAsync(providerAccountId, errorMessage, cancellationToken);

        public Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken) =>
            inner.RecordSuccessAsync(providerAccountId, cancellationToken);

        private void Require(AccessPolicy policy)
        {
            if (!gate.Allows(policy))
            {
                throw new UnauthorizedAccessException(gate.Denial(policy));
            }
        }
    }
}
