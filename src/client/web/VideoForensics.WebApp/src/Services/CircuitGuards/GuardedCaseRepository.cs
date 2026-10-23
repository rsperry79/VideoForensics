using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards case writes that the Cases and Case Details pages make in-process. Matches CaseEndpoints: create, edit,
    /// scope and item changes need Review; close and reopen need Admin. Reads stay open. The jamming-detection path
    /// runs from the server's own detector, not from an operator action, so it passes through.
    /// </summary>
    public sealed class GuardedCaseRepository(ICaseRepository inner, ICircuitOperatorGate gate) : ICaseRepository
    {
        public Task<ForensicCase> CreateAsync(
            string? caseNumber,
            string title,
            string? description,
            Guid? leadOperatorId,
            DateTime? scopeFromUtc,
            DateTime? scopeToUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string createdBy,
            CancellationToken ct)
        {
            Require(AccessPolicy.Review);
            return inner.CreateAsync(caseNumber, title, description, leadOperatorId, scopeFromUtc, scopeToUtc, deviceIds, createdBy, ct);
        }

        public Task<ForensicCase?> GetAsync(Guid id, CancellationToken ct)
        {
            return inner.GetAsync(id, ct);
        }

        public Task<ForensicCase?> GetByNumberAsync(string caseNumber, CancellationToken ct)
        {
            return inner.GetByNumberAsync(caseNumber, ct);
        }

        public Task<IReadOnlyList<ForensicCase>> ListAsync(CaseStatus? status, CancellationToken ct)
        {
            return inner.ListAsync(status, ct);
        }

        public Task UpdateDetailsAsync(Guid id, string title, string? description, Guid? leadOperatorId, string updatedBy, CancellationToken ct)
        {
            Require(AccessPolicy.Review);
            return inner.UpdateDetailsAsync(id, title, description, leadOperatorId, updatedBy, ct);
        }

        public Task SetScopeAsync(Guid id, DateTime? fromUtc, DateTime? toUtc, IReadOnlyCollection<Guid> deviceIds, string updatedBy, CancellationToken ct)
        {
            Require(AccessPolicy.Review);
            return inner.SetScopeAsync(id, fromUtc, toUtc, deviceIds, updatedBy, ct);
        }

        public Task<IReadOnlyList<Guid>> GetDeviceIdsAsync(Guid caseId, CancellationToken ct)
        {
            return inner.GetDeviceIdsAsync(caseId, ct);
        }

        public Task CloseAsync(Guid id, string closedBy, CancellationToken ct)
        {
            Require(AccessPolicy.Admin);
            return inner.CloseAsync(id, closedBy, ct);
        }

        public Task ReopenAsync(Guid id, string reopenedBy, CancellationToken ct)
        {
            Require(AccessPolicy.Admin);
            return inner.ReopenAsync(id, reopenedBy, ct);
        }

        public Task<CaseItem> AddItemAsync(Guid caseId, CaseItemKind kind, Guid targetId, string reason, string addedBy, CancellationToken ct)
        {
            Require(AccessPolicy.Review);
            return inner.AddItemAsync(caseId, kind, targetId, reason, addedBy, ct);
        }

        public Task RemoveItemAsync(Guid caseItemId, string removedBy, string reason, CancellationToken ct)
        {
            Require(AccessPolicy.Review);
            return inner.RemoveItemAsync(caseItemId, removedBy, reason, ct);
        }

        public Task<IReadOnlyList<CaseItem>> ListItemsAsync(Guid caseId, bool includeRemoved, CancellationToken ct)
        {
            return inner.ListItemsAsync(caseId, includeRemoved, ct);
        }

        public Task<IReadOnlyList<ForensicCase>> ListCasesContainingAsync(CaseItemKind kind, Guid targetId, CancellationToken ct)
        {
            return inner.ListCasesContainingAsync(kind, targetId, ct);
        }

        public Task<IReadOnlyList<ForensicCase>> ListForProviderAccountAsync(Guid providerAccountId, CancellationToken ct)
        {
            return inner.ListForProviderAccountAsync(providerAccountId, ct);
        }

        public Task<ForensicCase> CreateFromJammingDetectionAsync(JammingIncidentRecord jammingIncident, CancellationToken ct)
        {
            return inner.CreateFromJammingDetectionAsync(jammingIncident, ct);
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
