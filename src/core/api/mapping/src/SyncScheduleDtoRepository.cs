using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="ISyncScheduleRepository"/> over <see cref="ISyncScheduleWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class SyncScheduleDtoRepository(ISyncScheduleWriteRepository schedules) : ISyncScheduleRepository
    {
        /// <inheritdoc />
        public async Task<SyncScheduleDto?> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct) =>
            (await schedules.GetByProviderAccountIdAsync(providerAccountId, ct))?.ToDto();

        /// <inheritdoc />
        public Task UpsertAsync(SyncScheduleDto schedule, CancellationToken ct) =>
            schedules.UpsertAsync(schedule.ToDomain(), ct);
    }
}
