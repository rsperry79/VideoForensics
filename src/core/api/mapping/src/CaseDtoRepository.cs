using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>
    /// Server-side <see cref="ICaseRepository"/> that runs every case operation through <see cref="ICaseWriteRepository"/>
    /// and maps entities, enums and jamming incidents to their DTOs. Device ids are read per case so ForensicCaseDto carries them.
    /// </summary>
    public sealed class CaseDtoRepository(ICaseWriteRepository cases) : ICaseRepository
    {
        /// <inheritdoc />
        public async Task<ForensicCaseDto> CreateAsync(string? caseNumber, string title, string? description, Guid? leadOperatorId, DateTime? scopeFromUtc, DateTime? scopeToUtc, IReadOnlyCollection<Guid> deviceIds, string createdBy, CancellationToken ct)
        {
            ForensicCase created = await cases.CreateAsync(caseNumber, title, description, leadOperatorId, scopeFromUtc, scopeToUtc, deviceIds, createdBy, ct);
            return await ToDtoAsync(created, ct);
        }

        /// <inheritdoc />
        public async Task<ForensicCaseDto?> GetAsync(Guid id, CancellationToken ct)
        {
            ForensicCase? found = await cases.GetAsync(id, ct);
            return found is null ? null : await ToDtoAsync(found, ct);
        }

        /// <inheritdoc />
        public async Task<ForensicCaseDto?> GetByNumberAsync(string caseNumber, CancellationToken ct)
        {
            ForensicCase? found = await cases.GetByNumberAsync(caseNumber, ct);
            return found is null ? null : await ToDtoAsync(found, ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ForensicCaseDto>> ListAsync(CaseStatusDto? status, CancellationToken ct)
        {
            CaseStatus? entityStatus = status is null ? null : Enum.Parse<CaseStatus>(status.Value.ToString());
            return await ToDtosAsync(await cases.ListAsync(entityStatus, ct), ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ForensicCaseDto>> ListForProviderAccountAsync(Guid providerAccountId, CancellationToken ct) =>
            await ToDtosAsync(await cases.ListForProviderAccountAsync(providerAccountId, ct), ct);

        /// <inheritdoc />
        public Task UpdateDetailsAsync(Guid id, string title, string? description, Guid? leadOperatorId, string updatedBy, CancellationToken ct) =>
            cases.UpdateDetailsAsync(id, title, description, leadOperatorId, updatedBy, ct);

        /// <inheritdoc />
        public Task SetScopeAsync(Guid id, DateTime? fromUtc, DateTime? toUtc, IReadOnlyCollection<Guid> deviceIds, string updatedBy, CancellationToken ct) =>
            cases.SetScopeAsync(id, fromUtc, toUtc, deviceIds, updatedBy, ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<Guid>> GetDeviceIdsAsync(Guid caseId, CancellationToken ct) => cases.GetDeviceIdsAsync(caseId, ct);

        /// <inheritdoc />
        public Task CloseAsync(Guid id, string closedBy, CancellationToken ct) => cases.CloseAsync(id, closedBy, ct);

        /// <inheritdoc />
        public Task ReopenAsync(Guid id, string reopenedBy, CancellationToken ct) => cases.ReopenAsync(id, reopenedBy, ct);

        /// <inheritdoc />
        public async Task<CaseItemDto> AddItemAsync(Guid caseId, CaseItemKindDto kind, Guid targetId, string reason, string addedBy, CancellationToken ct)
        {
            CaseItem item = await cases.AddItemAsync(caseId, Enum.Parse<CaseItemKind>(kind.ToString()), targetId, reason, addedBy, ct);
            return item.ToDto();
        }

        /// <inheritdoc />
        public Task RemoveItemAsync(Guid caseItemId, string removedBy, string reason, CancellationToken ct) =>
            cases.RemoveItemAsync(caseItemId, removedBy, reason, ct);

        /// <inheritdoc />
        public async Task<IReadOnlyList<CaseItemDto>> ListItemsAsync(Guid caseId, bool includeRemoved, CancellationToken ct) =>
            (await cases.ListItemsAsync(caseId, includeRemoved, ct)).Select(i => i.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<ForensicCaseDto>> ListCasesContainingAsync(CaseItemKindDto kind, Guid targetId, CancellationToken ct) =>
            await ToDtosAsync(await cases.ListCasesContainingAsync(Enum.Parse<CaseItemKind>(kind.ToString()), targetId, ct), ct);

        /// <inheritdoc />
        public async Task<ForensicCaseDto> CreateFromJammingDetectionAsync(JammingIncidentDto jammingIncident, CancellationToken ct) =>
            await ToDtoAsync(await cases.CreateFromJammingDetectionAsync(jammingIncident.ToDomain(), ct), ct);

        private async Task<ForensicCaseDto> ToDtoAsync(ForensicCase entity, CancellationToken ct)
        {
            IReadOnlyList<Guid> deviceIds = await cases.GetDeviceIdsAsync(entity.Id, ct);
            return entity.ToDto(deviceIds);
        }

        private async Task<IReadOnlyList<ForensicCaseDto>> ToDtosAsync(IReadOnlyList<ForensicCase> entities, CancellationToken ct)
        {
            var result = new List<ForensicCaseDto>(entities.Count);
            foreach (ForensicCase entity in entities)
            {
                result.Add(await ToDtoAsync(entity, ct));
            }

            return result;
        }
    }
}
