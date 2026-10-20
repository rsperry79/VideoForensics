using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Lists the devices an account can see, through its location links.</summary>
    public interface IAccountDeviceRepository
    {
        /// <summary>
        /// Gets every device at a location the account is linked to. Devices are shared across accounts through
        /// <see cref="ProviderAccountLocation"/>, so this is a join, not a stored account ID on the device.
        /// </summary>
        Task<IReadOnlyList<Device>> ListByProviderAccountAsync(Guid providerAccountId, CancellationToken ct);
    }
}
