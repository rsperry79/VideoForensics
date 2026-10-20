using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for devices visible to a provider account.</summary>
    public class AccountDeviceRepository : IAccountDeviceRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<AccountDeviceRepository> _logger;

        /// <summary>Creates the repository.</summary>
        public AccountDeviceRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<AccountDeviceRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Device>> ListByProviderAccountAsync(Guid providerAccountId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

            // A device is visible when its location is linked to the account. Joined in the query so the account's
            // devices come back in one round trip, not one per location.
            IQueryable<Guid> linkedLocationIds = db.ProviderAccountLocations
                .Where(pal => pal.ProviderAccountId == providerAccountId)
                .Select(pal => pal.LocationId);

            List<Device> devices = await db.Devices
                .Where(d => linkedLocationIds.Contains(d.LocationId))
                .ToListAsync(ct);

            _logger.LogDebug("Listed {DeviceCount} device(s) for provider account", devices.Count);
            return devices;
        }
    }
}
