using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Uniview.Services
{
    /// <summary>
    /// Placeholder <see cref="ILiveViewCapableProvider"/> for Uniview devices.
    /// Uniview does not currently support live view via this library.
    /// This stub is registered so the UI/API always has a consistent "not supported" result
    /// rather than the capability simply not existing for Uniview devices.
    /// </summary>
    public class UniviewLiveViewProvider : ILiveViewCapableProvider
    {
        public Task<ILiveViewConnection> StartLiveViewAsync(string providerDeviceId, CancellationToken ct)
        {
            throw new NotSupportedException("Uniview does not support live view yet.");
        }
    }
}
