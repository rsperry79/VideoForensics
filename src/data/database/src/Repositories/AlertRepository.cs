using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Alert entities.</summary>
    public class AlertRepository : IAlertRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<AlertRepository> _logger;

        /// <summary>Initializes a new instance of the AlertRepository.</summary>
        public AlertRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<AlertRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<Alert> CreateAsync(Alert alert, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var newAlert = new Alert
                {
                    Id = alert.Id != Guid.Empty ? alert.Id : Guid.NewGuid(),
                    Title = alert.Title,
                    Description = alert.Description,
                    RelatedCaseId = alert.RelatedCaseId,
                    Status = alert.Status,
                    CreatedBy = alert.CreatedBy,
                    AlertType = alert.AlertType,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = null
                };

                _ = db.Alerts.Add(newAlert);
                _ = await db.SaveChangesAsync(ct);

                _logger.LogInformation("Alert {AlertId} created with type {AlertType} and status {Status}",
                    newAlert.Id, newAlert.AlertType, newAlert.Status);

                return newAlert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alert with type {AlertType}", alert.AlertType);
                throw;
            }
        }

        public async Task<Alert?> GetAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        }

        public async Task<Alert?> GetByCaseIdAsync(Guid caseId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Alerts.FirstOrDefaultAsync(a => a.RelatedCaseId == caseId, ct);
        }

        public async Task<IReadOnlyList<Alert>> ListAsync(string? status, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            IQueryable<Alert> query = db.Alerts.OrderByDescending(a => a.CreatedAtUtc);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            return await query.ToListAsync(ct);
        }
    }
}
