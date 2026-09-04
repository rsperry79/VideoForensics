using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Per-event-type urgent/not-urgent overrides for the Notifications settings screen (plan
    /// §5.6: "Each SecurityAuditLogEntry event type gets an IsUrgent classification... configurable
    /// per event type"). Backed by plain AppSettings rows (no new table) keyed
    /// <c>NotificationUrgentOverride:{eventType}</c> - absent means "use the caller's own default",
    /// not "not urgent", so every event type ships with its already-reviewed default urgency
    /// (plan §5.5/§5.12) until an operator explicitly overrides it.
    /// </summary>
    public interface IUrgencyOverrideStore
    {
        /// <summary>Null if no override is set for this event type (caller's own default applies).</summary>
        Task<bool?> GetOverrideAsync(string eventType, CancellationToken ct);
        Task<IReadOnlyDictionary<string, bool>> GetAllOverridesAsync(CancellationToken ct);
        Task SetOverrideAsync(string eventType, bool isUrgent, CancellationToken ct);
        Task ClearOverrideAsync(string eventType, CancellationToken ct);
    }

    public class UrgencyOverrideStore : IUrgencyOverrideStore
    {
        private const string KeyPrefix = "NotificationUrgentOverride:";

        private readonly IAppSettingRepository _settings;

        public UrgencyOverrideStore(IAppSettingRepository settings)
        {
            _settings = settings;
        }

        public async Task<bool?> GetOverrideAsync(string eventType, CancellationToken ct)
        {
            var value = await _settings.GetAsync(KeyPrefix + eventType, ct);
            return string.IsNullOrEmpty(value) ? null : bool.Parse(value);
        }

        public async Task<IReadOnlyDictionary<string, bool>> GetAllOverridesAsync(CancellationToken ct)
        {
            var all = await _settings.ListAsync(ct);
            return all
                .Where(s => s.Key.StartsWith(KeyPrefix, StringComparison.Ordinal))
                .ToDictionary(s => s.Key[KeyPrefix.Length..], s => bool.Parse(s.Value));
        }

        public Task SetOverrideAsync(string eventType, bool isUrgent, CancellationToken ct)
            => _settings.SetAsync(KeyPrefix + eventType, isUrgent.ToString(), ct);

        public Task ClearOverrideAsync(string eventType, CancellationToken ct)
            => _settings.DeleteAsync(KeyPrefix + eventType, ct);
    }
}
