using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    public interface IAppSettingRepository
    {
        Task<string?> GetAsync(string key, CancellationToken ct);
        Task SetAsync(string key, string value, CancellationToken ct);
        Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken ct);
        Task DeleteAsync(string key, CancellationToken ct);
        Task ClearAllAsync(CancellationToken ct);
    }
}
