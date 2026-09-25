using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for forensic cases with chain-of-custody logging.</summary>
    public class CaseRepository : ICaseRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly IActionLogRepository _actionLogRepository;
        private readonly ILogger<CaseRepository> _logger;

        /// <summary>Initializes a new instance of the CaseRepository.</summary>
        public CaseRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            IActionLogRepository actionLogRepository,
            ILogger<CaseRepository> logger)
        {
            _factory = factory;
            _actionLogRepository = actionLogRepository;
            _logger = logger;
        }

        public async Task<ForensicCase> CreateAsync(
            string caseNumber,
            string title,
            string? description,
            Guid? leadOperatorId,
            DateTime? scopeFromUtc,
            DateTime? scopeToUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string createdBy,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                // Check for duplicate case number
                bool exists = await db.Cases.AnyAsync(c => c.CaseNumber == caseNumber, ct);
                if (exists)
                {
                    throw new InvalidOperationException($"A case with number '{caseNumber}' already exists.");
                }

                var forensicCase = new ForensicCase
                {
                    Id = Guid.NewGuid(),
                    CaseNumber = caseNumber,
                    Title = title,
                    Description = description,
                    LeadOperatorId = leadOperatorId,
                    Status = CaseStatus.Open,
                    CreatedBy = createdBy,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    ScopeFromUtc = scopeFromUtc,
                    ScopeToUtc = scopeToUtc
                };

                _ = db.Cases.Add(forensicCase);

                // Add device scope entries
                var deviceSet = new List<CaseDevice>();
                foreach (var deviceId in deviceIds)
                {
                    deviceSet.Add(new CaseDevice
                    {
                        CaseId = forensicCase.Id,
                        DeviceId = deviceId,
                        AddedAtUtc = DateTime.UtcNow
                    });
                }
                if (deviceSet.Count > 0)
                {
                    db.CaseDevices.AddRange(deviceSet);
                }

                _ = await db.SaveChangesAsync(ct);

                // Append custody entry
                _ = await _actionLogRepository.AppendAsync(
                    createdBy,
                    ActorType.Human,
                    "CreateCase",
                    "Case",
                    forensicCase.Id,
                    $"Case number: {caseNumber}, Title: {title}, Devices: {deviceIds.Count}",
                    ct);

                _logger.LogInformation("Case {CaseNumber} created by {CreatedBy}", caseNumber, createdBy);

                return forensicCase;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating case {CaseNumber}", caseNumber);
                throw;
            }
        }

        public async Task<ForensicCase?> GetAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Cases.FirstOrDefaultAsync(c => c.Id == id, ct);
        }

        public async Task<ForensicCase?> GetByNumberAsync(string caseNumber, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Cases.FirstOrDefaultAsync(c => c.CaseNumber == caseNumber, ct);
        }

        public async Task<IReadOnlyList<ForensicCase>> ListAsync(CaseStatus? status, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            IQueryable<ForensicCase> query = db.Cases.OrderByDescending(c => c.CreatedAtUtc);

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            return await query.ToListAsync(ct);
        }

        public async Task UpdateDetailsAsync(
            Guid id,
            string title,
            string? description,
            Guid? leadOperatorId,
            string updatedBy,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                ForensicCase? forensicCase = await db.Cases.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (forensicCase is null)
                {
                    throw new InvalidOperationException($"Case {id} not found.");
                }

                if (forensicCase.Status == CaseStatus.Closed)
                {
                    throw new InvalidOperationException($"Cannot update a closed case {id}.");
                }

                forensicCase.Title = title;
                forensicCase.Description = description;
                forensicCase.LeadOperatorId = leadOperatorId;
                forensicCase.UpdatedAtUtc = DateTime.UtcNow;

                _ = await db.SaveChangesAsync(ct);

                _ = await _actionLogRepository.AppendAsync(
                    updatedBy,
                    ActorType.Human,
                    "UpdateCase",
                    "Case",
                    id,
                    $"Title: {title}",
                    ct);

                _logger.LogInformation("Case {CaseId} updated by {UpdatedBy}", id, updatedBy);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating case {CaseId}", id);
                throw;
            }
        }

        public async Task SetScopeAsync(
            Guid id,
            DateTime? fromUtc,
            DateTime? toUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string updatedBy,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                ForensicCase? forensicCase = await db.Cases.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (forensicCase is null)
                {
                    throw new InvalidOperationException($"Case {id} not found.");
                }

                if (forensicCase.Status == CaseStatus.Closed)
                {
                    throw new InvalidOperationException($"Cannot update scope of a closed case {id}.");
                }

                forensicCase.ScopeFromUtc = fromUtc;
                forensicCase.ScopeToUtc = toUtc;
                forensicCase.UpdatedAtUtc = DateTime.UtcNow;

                // Replace device set
                IEnumerable<CaseDevice> existingDevices = db.CaseDevices.Where(cd => cd.CaseId == id);
                db.CaseDevices.RemoveRange(existingDevices);

                var newDevices = new List<CaseDevice>();
                foreach (var deviceId in deviceIds)
                {
                    newDevices.Add(new CaseDevice
                    {
                        CaseId = id,
                        DeviceId = deviceId,
                        AddedAtUtc = DateTime.UtcNow
                    });
                }
                if (newDevices.Count > 0)
                {
                    db.CaseDevices.AddRange(newDevices);
                }

                _ = await db.SaveChangesAsync(ct);

                string details = $"Scope: {fromUtc:O} to {toUtc:O}, Devices: {deviceIds.Count}";
                _ = await _actionLogRepository.AppendAsync(
                    updatedBy,
                    ActorType.Human,
                    "SetCaseScope",
                    "Case",
                    id,
                    details,
                    ct);

                _logger.LogInformation("Case {CaseId} scope updated by {UpdatedBy}", id, updatedBy);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting case scope for {CaseId}", id);
                throw;
            }
        }

        public async Task<IReadOnlyList<Guid>> GetDeviceIdsAsync(Guid caseId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.CaseDevices
                .Where(cd => cd.CaseId == caseId)
                .Select(cd => cd.DeviceId)
                .ToListAsync(ct);
        }

        public async Task CloseAsync(Guid id, string closedBy, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                ForensicCase? forensicCase = await db.Cases.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (forensicCase is null)
                {
                    throw new InvalidOperationException($"Case {id} not found.");
                }

                forensicCase.Status = CaseStatus.Closed;
                forensicCase.ClosedBy = closedBy;
                forensicCase.ClosedAtUtc = DateTime.UtcNow;
                forensicCase.UpdatedAtUtc = DateTime.UtcNow;

                _ = await db.SaveChangesAsync(ct);

                _ = await _actionLogRepository.AppendAsync(
                    closedBy,
                    ActorType.Human,
                    "CloseCase",
                    "Case",
                    id,
                    string.Empty,
                    ct);

                _logger.LogInformation("Case {CaseId} closed by {ClosedBy}", id, closedBy);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing case {CaseId}", id);
                throw;
            }
        }

        public async Task ReopenAsync(Guid id, string reopenedBy, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                ForensicCase? forensicCase = await db.Cases.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (forensicCase is null)
                {
                    throw new InvalidOperationException($"Case {id} not found.");
                }

                forensicCase.Status = CaseStatus.Open;
                forensicCase.ClosedBy = null;
                forensicCase.ClosedAtUtc = null;
                forensicCase.UpdatedAtUtc = DateTime.UtcNow;

                _ = await db.SaveChangesAsync(ct);

                _ = await _actionLogRepository.AppendAsync(
                    reopenedBy,
                    ActorType.Human,
                    "ReopenCase",
                    "Case",
                    id,
                    string.Empty,
                    ct);

                _logger.LogInformation("Case {CaseId} reopened by {ReopenedBy}", id, reopenedBy);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reopening case {CaseId}", id);
                throw;
            }
        }

        public async Task<CaseItem> AddItemAsync(
            Guid caseId,
            CaseItemKind kind,
            Guid targetId,
            string reason,
            string addedBy,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                ForensicCase? forensicCase = await db.Cases.FirstOrDefaultAsync(c => c.Id == caseId, ct);
                if (forensicCase is null)
                {
                    throw new InvalidOperationException($"Case {caseId} not found.");
                }

                if (forensicCase.Status == CaseStatus.Closed)
                {
                    throw new InvalidOperationException($"Cannot add items to a closed case {caseId}.");
                }

                // Check for duplicate active pin
                CaseItem? existingItem = kind switch
                {
                    CaseItemKind.Event => await db.CaseItems.FirstOrDefaultAsync(
                        ci => ci.CaseId == caseId && ci.EventId == targetId && ci.RemovedAtUtc == null, ct),
                    CaseItemKind.Media => await db.CaseItems.FirstOrDefaultAsync(
                        ci => ci.CaseId == caseId && ci.MediaItemId == targetId && ci.RemovedAtUtc == null, ct),
                    _ => null
                };

                if (existingItem is not null)
                {
                    throw new InvalidOperationException(
                        $"Target {kind} {targetId} is already actively pinned to case {caseId}.");
                }

                string? mediaSha256AtAdd = null;
                if (kind == CaseItemKind.Media)
                {
                    MediaItem? mediaItem = await db.MediaItems.FirstOrDefaultAsync(m => m.Id == targetId, ct);
                    if (mediaItem is not null)
                    {
                        mediaSha256AtAdd = mediaItem.Sha256Hash;
                    }
                    // Note: We don't validate that the media item exists, as it may be added later.
                    // FK constraint will be enforced by the database.
                }

                var caseItem = new CaseItem
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    Kind = kind,
                    EventId = kind == CaseItemKind.Event ? targetId : null,
                    MediaItemId = kind == CaseItemKind.Media ? targetId : null,
                    Reason = reason,
                    AddedBy = addedBy,
                    AddedAtUtc = DateTime.UtcNow,
                    MediaSha256AtAdd = mediaSha256AtAdd
                };

                _ = db.CaseItems.Add(caseItem);
                _ = await db.SaveChangesAsync(ct);

                _ = await _actionLogRepository.AppendAsync(
                    addedBy,
                    ActorType.Human,
                    "AddCaseItem",
                    "Case",
                    caseId,
                    $"Kind: {kind}, Target: {targetId}, Reason: {reason}",
                    ct);

                _logger.LogInformation(
                    "Case {CaseId} item added: {Kind} {TargetId} by {AddedBy}",
                    caseId, kind, targetId, addedBy);

                return caseItem;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to case {CaseId}", caseId);
                throw;
            }
        }

        public async Task RemoveItemAsync(Guid caseItemId, string removedBy, string reason, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                CaseItem? caseItem = await db.CaseItems.FirstOrDefaultAsync(ci => ci.Id == caseItemId, ct);
                if (caseItem is null)
                {
                    throw new InvalidOperationException($"Case item {caseItemId} not found.");
                }

                caseItem.RemovedBy = removedBy;
                caseItem.RemovedAtUtc = DateTime.UtcNow;
                caseItem.RemovalReason = reason;

                _ = await db.SaveChangesAsync(ct);

                _ = await _actionLogRepository.AppendAsync(
                    removedBy,
                    ActorType.Human,
                    "RemoveCaseItem",
                    "Case",
                    caseItem.CaseId,
                    $"Item: {caseItemId}, Reason: {reason}",
                    ct);

                _logger.LogInformation("Case item {CaseItemId} removed by {RemovedBy}", caseItemId, removedBy);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing case item {CaseItemId}", caseItemId);
                throw;
            }
        }

        public async Task<IReadOnlyList<CaseItem>> ListItemsAsync(Guid caseId, bool includeRemoved, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            IQueryable<CaseItem> query = db.CaseItems.Where(ci => ci.CaseId == caseId);

            if (!includeRemoved)
            {
                query = query.Where(ci => ci.RemovedAtUtc == null);
            }

            return await query.ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ForensicCase>> ListCasesContainingAsync(CaseItemKind kind, Guid targetId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

            IQueryable<CaseItem> items = kind switch
            {
                CaseItemKind.Event => db.CaseItems.Where(ci => ci.EventId == targetId && ci.RemovedAtUtc == null),
                CaseItemKind.Media => db.CaseItems.Where(ci => ci.MediaItemId == targetId && ci.RemovedAtUtc == null),
                _ => db.CaseItems.Where(ci => false)
            };

            var caseIds = await items.Select(ci => ci.CaseId).Distinct().ToListAsync(ct);

            return await db.Cases
                .Where(c => caseIds.Contains(c.Id))
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(ct);
        }
    }
}
