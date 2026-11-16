using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.ReferenceData;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Writes discovered provider device models through a short-lived unit of work per call.</summary>
    public class DeviceModelRepository : IDeviceModelRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>Creates the repository.</summary>
        public DeviceModelRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task EnsureDiscoveredAsync(string provider, string kind, string family, CancellationToken ct)
        {
            return ReferenceDataSeeder.EnsureDiscoveredModelAsync(_unitOfWork, provider, kind, family, ct);
        }
    }
}
