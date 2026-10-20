using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.ReferenceData;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Writes discovered provider device models through a short-lived DbContext per call.</summary>
    public class DeviceModelRepository : IDeviceModelRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        /// <summary>Creates the repository.</summary>
        public DeviceModelRepository(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        /// <inheritdoc />
        public Task EnsureDiscoveredAsync(string provider, string kind, string family, CancellationToken ct)
        {
            return ReferenceDataSeeder.EnsureDiscoveredModelAsync(_factory, provider, kind, family, ct);
        }
    }
}
