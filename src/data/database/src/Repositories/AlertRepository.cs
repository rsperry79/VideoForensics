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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AlertRepository> _logger;

        /// <summary>Initializes a new instance of the AlertRepository.</summary>
        public AlertRepository(IUnitOfWork unitOfWork, ILogger<AlertRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Alert> CreateAsync(Alert alert, CancellationToken ct)
        {
            try
            {
                Alert newAlert = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    var created = new Alert
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

                    _ = db.Alerts.Add(created);
                    _ = await db.SaveChangesAsync(ct);
                    return created;
                }, ct);

                _logger.LogDebug("Alert {AlertId} created with type {AlertType} and status {Status}",
                    newAlert.Id, newAlert.AlertType, newAlert.Status);

                return newAlert;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error creating alert with type {AlertType}: {Message} ({ErrorType})", alert.AlertType, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task<Alert?> GetAsync(Guid id, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Alerts.FirstOrDefaultAsync(a => a.Id == id, ct), ct);
        }

        public async Task<Alert?> GetByCaseIdAsync(Guid caseId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Alerts.FirstOrDefaultAsync(a => a.RelatedCaseId == caseId, ct), ct);
        }

        public async Task<IReadOnlyList<Alert>> ListAsync(string? status, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<IReadOnlyList<Alert>>(async context =>
            {
                IQueryable<Alert> query = context.Db().Alerts.OrderByDescending(a => a.CreatedAtUtc);

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(a => a.Status == status);
                }

                return await query.ToListAsync(ct);
            }, ct);
        }
    }
}
