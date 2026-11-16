namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Provider sync schedules, with DTO signatures. Client hosts use this interface.</summary>
    public interface ISyncScheduleRepository
    {
        /// <summary>Gets the sync schedule for a provider account, or null when none is saved.</summary>
        Task<SyncScheduleDto?> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Creates or replaces the sync schedule, including its jamming windows.</summary>
        Task UpsertAsync(SyncScheduleDto schedule, CancellationToken ct);
    }
}
