using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="IOperatorPreferencesRepository"/> over <see cref="IOperatorPreferencesWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class OperatorPreferencesDtoRepository(IOperatorPreferencesWriteRepository store) : IOperatorPreferencesRepository
    {
        /// <inheritdoc />
        public async Task<OperatorPreferencesDto?> GetAsync(Guid operatorId, CancellationToken ct) =>
            (await store.GetAsync(operatorId, ct))?.ToDto();

        /// <inheritdoc />
        public Task UpsertAsync(OperatorPreferencesDto preferences, CancellationToken ct) =>
            store.UpsertAsync(preferences.ToDomain(), ct);

        /// <inheritdoc />
        public async Task<OperatorPreferencesDto> SetUiModeAsync(Guid operatorId, string uiMode, bool locked, CancellationToken ct) =>
            (await store.SetUiModeAsync(operatorId, uiMode, locked, ct)).ToDto();

        /// <inheritdoc />
        public Task SetEncryptionWarningDismissedAsync(Guid operatorId, bool dismissed, CancellationToken ct) =>
            store.SetEncryptionWarningDismissedAsync(operatorId, dismissed, ct);
    }
}
