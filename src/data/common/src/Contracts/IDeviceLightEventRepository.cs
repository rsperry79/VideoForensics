using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for the append-only device light-on events.</summary>
    public interface IDeviceLightEventRepository
    {
        /// <summary>
        /// Records an Observed event when the reported seconds remaining is above zero and the device's light was not already
        /// considered on. The light counts as already on while the latest on event (IsOn true) has seconds remaining above zero and its
        /// time plus those seconds is still after <paramref name="observedAtUtc"/>. Returns true when an event was written.
        /// A null or zero value writes nothing.
        /// </summary>
        Task<bool> RecordObservedAsync(Guid deviceId, int? secondsRemaining, DateTime observedAtUtc, CancellationToken ct);

        /// <summary>
        /// Records the reported on/off state of a light that reports no duration. On writes an Observed event with no seconds unless the
        /// latest event is already an open-ended Observed on; off writes an Observed event with IsOn false and no seconds (the off marker) only when
        /// the latest event is an open-ended Observed on. Returns true when an event was written.
        /// </summary>
        Task<bool> RecordObservedStateAsync(Guid deviceId, bool on, DateTime observedAtUtc, CancellationToken ct);

        /// <summary>Appends a Command event for a light switched on by this application.</summary>
        Task AddCommandAsync(Guid deviceId, DateTime occurredAtUtc, int? secondsRemaining, CancellationToken ct);

        /// <summary>Lists the light events for a device, newest first. With <paramref name="onEventsOnly"/> the off markers (IsOn false) are left out.</summary>
        Task<IReadOnlyList<DeviceLightEvent>> ListByDeviceAsync(Guid deviceId, CancellationToken ct, bool onEventsOnly = false);
    }
}
