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
        private const string SystemActor = "System";

        private readonly TimeProvider _timeProvider;

        /// <summary>Initializes a new instance of the CaseRepository.</summary>
        public CaseRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            IActionLogRepository actionLogRepository,
            ILogger<CaseRepository> logger,
            TimeProvider? timeProvider = null)
        {
            _factory = factory;
            _actionLogRepository = actionLogRepository;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Generates a unique case number with the pattern {prefix}-yyyy-mm-dd-hh-mm-{index}.
        /// The index is the maximum existing index for the given prefix in the same minute, plus one.
        /// This ensures collision-safe generation even with concurrent creates.
        /// </summary>
        /// <param name="prefix">Case number prefix (e.g., "detected", "suspected", "manual")</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Generated case number string</returns>
        public async Task<string> GenerateCaseNumber(string prefix, CancellationToken ct)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            DateTime startOfMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
            DateTime endOfMinute = startOfMinute.AddMinutes(1);
            string timePrefix = now.ToString("yyyy-MM-dd-HH-mm");
            string searchPattern = $"{prefix}-{timePrefix}-";

            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            
            // Get all case numbers that match the prefix and time window, extract indices
            var matchingNumbers = await db.Cases
                .Where(c => c.CreatedAtUtc >= startOfMinute && c.CreatedAtUtc < endOfMinute && c.CaseNumber.StartsWith(searchPattern))
                .Select(c => c.CaseNumber)
                .ToListAsync(ct);

            int maxIndex = -1;
            foreach (var number in matchingNumbers)
            {
                if (int.TryParse(number.Substring(searchPattern.Length), out int index))
                {
                    maxIndex = Math.Max(maxIndex, index);
                }
            }

            return $"{searchPattern}{maxIndex + 1}";
        }

        /// <summary>
        /// Creates a forensic case auto-triggered by jamming detection.
        /// Prefix is determined by confidence level: "detected" if High/Definite, else "suspected".
        /// Scope is set from jamming event time window.
        /// Idempotent: if the incident already has a CaseId and that case exists, returns it without creating a duplicate.
        /// Atomically creates the case, device scope, and a linked Alert in a single SaveChangesAsync call.
        /// </summary>
        /// <param name="jammingEvent">The jamming incident record that triggered case creation</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The created or existing forensic case</returns>
        public async Task<ForensicCase> CreateFromJammingDetectionAsync(
            JammingIncidentRecord jammingEvent,
            CancellationToken ct)
        {
            // Idempotency check: if incident already has a case, return it
            if (jammingEvent.CaseId.HasValue && jammingEvent.CaseId != Guid.Empty)
            {
                ForensicCase? existingCase = await GetAsync(jammingEvent.CaseId.Value, ct);
                if (existingCase is not null)
                {
                    _logger.LogInformation("Jamming incident {IncidentId} already has case {CaseId}; returning existing case", jammingEvent.Id, jammingEvent.CaseId);
                    return existingCase;
                }
            }

            // Determine prefix and title based on confidence level
            string prefix = jammingEvent.Confidence >= JammingConfidenceLevel.High ? "detected" : "suspected";
            string alertTitle = jammingEvent.Confidence >= JammingConfidenceLevel.High
                ? "Jamming Detected"
                : "Jamming Suspected";

            string caseDescription = BuildAlertDescription(jammingEvent);

            // Create case with callback that atomically creates alert and links incident
            return await CreateCoreAsync(
                null,
                prefix,
                alertTitle,
                caseDescription,
                leadOperatorId: null,
                jammingEvent.StartUtc,
                jammingEvent.EndUtc,
                new[] { jammingEvent.DeviceId },
                SystemActor,
                ct,
                onBeforeSave: (db, forensicCase) =>
                {
                    // Create the alert within the same transaction
                    var alert = new Alert
                    {
                        Id = Guid.NewGuid(),
                        Title = alertTitle,
                        Description = caseDescription,
                        RelatedCaseId = forensicCase.Id,
                        Status = "Open",
                        CreatedBy = SystemActor,
                        AlertType = "JammingDetection",
                        CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
                        UpdatedAtUtc = null
                    };
                    _ = db.Alerts.Add(alert);

                    // Link the incident to the case
                    jammingEvent.CaseId = forensicCase.Id;
                    
                    // Note: jammingEvent is already tracked by db.JammingIncidents if it was loaded from this context.
                    // If not tracked, we need to attach and mark as modified.
                    var incident = db.JammingIncidentRecords.Find(jammingEvent.Id);
                    if (incident is not null)
                    {
                        incident.CaseId = forensicCase.Id;
                    }
                    else
                    {
                        // Incident not in DB yet or from another context; log and continue
                        // The callback cannot add it because we don't know if it exists
                        _logger.LogWarning("Jamming incident {IncidentId} not found in database context; skipping CaseId link", jammingEvent.Id);
                    }
                });
        }

        /// <summary>
        /// Builds a formatted description for a jamming incident alert.
        /// Includes device ID, time window, degradation dB, affected event count, and confidence.
        /// Description is capped at 4000 characters.
        /// </summary>
        private string BuildAlertDescription(JammingIncidentRecord jammingEvent)
        {
            string description = $"Jamming incident on device {jammingEvent.DeviceId} " +
                $"from {jammingEvent.StartUtc:O} to {jammingEvent.EndUtc:O}. " +
                $"Average degradation: {jammingEvent.AverageDegradationDb:F2} dB. " +
                $"Affected events: {jammingEvent.AffectedEventCount}. " +
                $"Confidence: {jammingEvent.Confidence}. " +
                (string.IsNullOrEmpty(jammingEvent.Notes) ? string.Empty : $"Notes: {jammingEvent.Notes}");

            // Cap at 4000 chars as per Alert schema
            if (description.Length > 4000)
            {
                description = description.Substring(0, 3997) + "...";
            }

            return description;
        }

        public Task<ForensicCase> CreateAsync(
            string? caseNumber,
            string title,
            string? description,
            Guid? leadOperatorId,
            DateTime? scopeFromUtc,
            DateTime? scopeToUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string createdBy,
            CancellationToken ct)
            => CreateCoreAsync(caseNumber, "manual", title, description, leadOperatorId, scopeFromUtc, scopeToUtc, deviceIds, createdBy, ct);

        private async Task<ForensicCase> CreateCoreAsync(
            string? explicitNumber,
            string generatedPrefix,
            string title,
            string? description,
            Guid? leadOperatorId,
            DateTime? scopeFromUtc,
            DateTime? scopeToUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string createdBy,
            CancellationToken ct,
            Action<VideoForensicsDbContext, ForensicCase>? onBeforeSave = null)
        {
            bool wasGenerated = string.IsNullOrWhiteSpace(explicitNumber);
            string finalCaseNumber = wasGenerated ? await GenerateCaseNumber(generatedPrefix, ct) : explicitNumber!;

            // For auto-generated numbers, retry up to 5 times on collision
            int maxRetries = wasGenerated ? 5 : 1;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
                try
                {
                    // Check for duplicate case number
                    bool exists = await db.Cases.AnyAsync(c => c.CaseNumber == finalCaseNumber, ct);
                    if (exists)
                    {
                        if (wasGenerated && attempt < maxRetries - 1)
                        {
                            // Retry with a new generated number
                            finalCaseNumber = await GenerateCaseNumber(generatedPrefix, ct);
                            continue;
                        }
                        throw new InvalidOperationException($"A case with number '{finalCaseNumber}' already exists.");
                    }

                    var forensicCase = new ForensicCase
                    {
                        Id = Guid.NewGuid(),
                        CaseNumber = finalCaseNumber,
                        Title = title,
                        Description = description,
                        LeadOperatorId = leadOperatorId,
                        Status = CaseStatus.Open,
                        CreatedBy = createdBy,
                        CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
                        UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
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
                            AddedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
                        });
                    }
                    if (deviceSet.Count > 0)
                    {
                        db.CaseDevices.AddRange(deviceSet);
                    }

                    ActorType actorType = createdBy == SystemActor ? ActorType.System : ActorType.Human;

                    onBeforeSave?.Invoke(db, forensicCase);

                    _ = await db.SaveChangesAsync(ct);

                    // Append custody entry
                    _ = await _actionLogRepository.AppendAsync(
                        createdBy,
                        actorType,
                        "CreateCase",
                        "Case",
                        forensicCase.Id,
                        $"Case number: {finalCaseNumber}, Title: {title}, Devices: {deviceIds.Count}",
                        ct);

                    _logger.LogInformation("Case {CaseNumber} created by {CreatedBy}", finalCaseNumber, createdBy);

                    return forensicCase;
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (DbUpdateException) when (wasGenerated && attempt < maxRetries - 1)
                {
                    // Another writer can take the number between the pre-check and the insert; only a
                    // number collision is retried, so FK and other constraint failures still propagate.
                    await using VideoForensicsDbContext check = await _factory.CreateDbContextAsync(ct);
                    if (!await check.Cases.AnyAsync(c => c.CaseNumber == finalCaseNumber, ct))
                    {
                        throw;
                    }

                    finalCaseNumber = await GenerateCaseNumber(generatedPrefix, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating case {CaseNumber}", finalCaseNumber);
                    throw;
                }
            }

            throw new InvalidOperationException("Failed to create case after maximum retries.");
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
                        AddedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
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
                    AddedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
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


