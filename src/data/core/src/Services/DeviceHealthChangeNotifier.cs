using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Default notifier for hosts with no live views. Does nothing.</summary>
    public sealed class NullDeviceHealthChangeNotifier : IDeviceHealthChangeNotifier
    {
        /// <inheritdoc />
        public Task DeviceHealthRecordedAsync(Guid deviceId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
