using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Wyze.Services
{
    /// <summary>
    /// Placeholder <see cref="ILiveViewCapableProvider"/> for Wyze devices.
    /// Wyze does not currently support live view via this library.
    /// This stub is registered so the UI/API always has a consistent "not supported" result
    /// rather than the capability simply not existing for Wyze devices.
    /// </summary>
    public class WyzeLiveViewProvider : ILiveViewCapableProvider
    {
        public Task<ILiveViewConnection> StartLiveViewAsync(string providerDeviceId, CancellationToken ct)
        {
            throw new NotSupportedException("Wyze does not support live view yet.");
        }
    }
}
