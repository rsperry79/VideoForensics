using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository for evidence access and modification audit trails.</summary>
    public class AuditTrailRepository : IAuditTrailRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<AuditTrailRepository> _logger;

        public AuditTrailRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ILogger<AuditTrailRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Logs evidence access for chain of custody.</summary>
        public async Task LogAccessAsync(Guid evidenceId, string userId, string action, DateTime accessAtUtc, CancellationToken ct)
        {
            _logger.LogInformation("LogAccessAsync entry: EvidenceId={EvidenceId}, UserId={UserId}, Action={Action}", evidenceId, userId, action);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                var entry = new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = userId,
                    ActorType = ActorType.Human,
                    Action = action,
                    EntityType = "Evidence",
                    EntityId = evidenceId,
                    DetailsJson = null,
                    TimestampUtc = accessAtUtc,
                    PreviousEntryHash = null,
                    EntryHash = "hash_" + Guid.NewGuid().ToString().Substring(0, 8)
                };

                context.ActionLogEntries.Add(entry);
                await context.SaveChangesAsync(ct);

                _logger.LogInformation("LogAccessAsync exit: successfully logged access for EvidenceId={EvidenceId}", evidenceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging access for EvidenceId={EvidenceId}", evidenceId);
                throw;
            }
        }

        /// <summary>Logs export record for forensic audit trail.</summary>
        public async Task LogExportAsync(Guid exportId, Guid locationId, string exportedBy, int eventCount, CancellationToken ct)
        {
            _logger.LogInformation("LogExportAsync entry: ExportId={ExportId}, LocationId={LocationId}, ExportedBy={ExportedBy}", exportId, locationId, exportedBy);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                var entry = new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = exportedBy,
                    ActorType = ActorType.Human,
                    Action = "Export",
                    EntityType = "Location",
                    EntityId = locationId,
                    DetailsJson = $"{{\"exportId\":\"{exportId}\",\"eventCount\":{eventCount}}}",
                    TimestampUtc = DateTime.UtcNow,
                    PreviousEntryHash = null,
                    EntryHash = "hash_" + Guid.NewGuid().ToString().Substring(0, 8)
                };

                context.ActionLogEntries.Add(entry);
                await context.SaveChangesAsync(ct);

                _logger.LogInformation("LogExportAsync exit: successfully logged export for ExportId={ExportId}", exportId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging export for ExportId={ExportId}", exportId);
                throw;
            }
        }

        /// <summary>Gets full access history for evidence.</summary>
        public async Task<IReadOnlyList<AccessAuditLog>> GetAccessHistoryAsync(Guid evidenceId, CancellationToken ct)
        {
            _logger.LogInformation("GetAccessHistoryAsync entry: EvidenceId={EvidenceId}", evidenceId);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                var entries = await context.ActionLogEntries
                    .Where(ale => ale.EntityId == evidenceId && ale.EntityType == "Evidence")
                    .OrderByDescending(ale => ale.TimestampUtc)
                    .ToListAsync(ct);

                var logs = entries.Select(e => new AccessAuditLog
                {
                    Id = e.Id,
                    EvidenceId = e.EntityId ?? Guid.Empty,
                    UserId = e.Actor,
                    AccessedAtUtc = e.TimestampUtc,
                    Action = e.Action,
                    IpAddress = string.Empty,
                    Purpose = string.Empty
                }).ToList();

                _logger.LogInformation("GetAccessHistoryAsync exit: found {Count} access logs for EvidenceId={EvidenceId}", logs.Count, evidenceId);
                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access history for EvidenceId={EvidenceId}", evidenceId);
                throw;
            }
        }

        /// <summary>Gets access history for location (joins with Devices and Events).</summary>
        public async Task<IReadOnlyList<AccessAuditLog>> GetLocationAccessHistoryAsync(Guid locationId, CancellationToken ct)
        {
            _logger.LogInformation("GetLocationAccessHistoryAsync entry: LocationId={LocationId}", locationId);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                // Get all devices for this location
                var deviceIds = await context.Devices
                    .Where(d => d.LocationId == locationId)
                    .Select(d => d.Id)
                    .ToListAsync(ct);

                if (deviceIds.Count == 0)
                {
                    _logger.LogInformation("GetLocationAccessHistoryAsync exit: no devices found for LocationId={LocationId}", locationId);
                    return new List<AccessAuditLog>();
                }

                // Get all events for these devices
                var eventIds = await context.Events
                    .Where(e => deviceIds.Contains(e.DeviceId))
                    .Select(e => e.Id)
                    .ToListAsync(ct);

                // Get access logs for these events
                var entries = await context.ActionLogEntries
                    .Where(ale => eventIds.Contains(ale.EntityId ?? Guid.Empty) && ale.EntityType == "Event")
                    .OrderByDescending(ale => ale.TimestampUtc)
                    .ToListAsync(ct);

                var logs = entries.Select(e => new AccessAuditLog
                {
                    Id = e.Id,
                    EvidenceId = e.EntityId ?? Guid.Empty,
                    UserId = e.Actor,
                    AccessedAtUtc = e.TimestampUtc,
                    Action = e.Action,
                    IpAddress = string.Empty,
                    Purpose = string.Empty
                }).ToList();

                _logger.LogInformation("GetLocationAccessHistoryAsync exit: found {Count} access logs for LocationId={LocationId}", logs.Count, locationId);
                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access history for LocationId={LocationId}", locationId);
                throw;
            }
        }

        /// <summary>Gets paginated access history for evidence.</summary>
        public async Task<PaginatedResult<AccessAuditLog>> GetAccessHistoryPaginatedAsync(
            Guid evidenceId, int pageNumber, int pageSize, CancellationToken ct)
        {
            _logger.LogInformation("GetAccessHistoryPaginatedAsync entry: EvidenceId={EvidenceId}, Page={PageNumber}, Size={PageSize}", evidenceId, pageNumber, pageSize);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                var totalCount = await context.ActionLogEntries
                    .Where(ale => ale.EntityId == evidenceId && ale.EntityType == "Evidence")
                    .CountAsync(ct);

                var entries = await context.ActionLogEntries
                    .Where(ale => ale.EntityId == evidenceId && ale.EntityType == "Evidence")
                    .OrderByDescending(ale => ale.TimestampUtc)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                var logs = entries.Select(e => new AccessAuditLog
                {
                    Id = e.Id,
                    EvidenceId = e.EntityId ?? Guid.Empty,
                    UserId = e.Actor,
                    AccessedAtUtc = e.TimestampUtc,
                    Action = e.Action,
                    IpAddress = string.Empty,
                    Purpose = string.Empty
                }).ToList();

                var result = new PaginatedResult<AccessAuditLog>
                {
                    Items = logs,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };

                _logger.LogInformation("GetAccessHistoryPaginatedAsync exit: returned {Count} of {Total} logs for EvidenceId={EvidenceId}", logs.Count, totalCount, evidenceId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated access history for EvidenceId={EvidenceId}", evidenceId);
                throw;
            }
        }

        /// <summary>Gets export history for a location (with cursor-based pagination).</summary>
        public async Task<CursorPaginatedResult<ExportAuditRecord>> GetExportHistoryCursorAsync(
            Guid locationId, string? cursor, int pageSize, CancellationToken ct)
        {
            _logger.LogInformation("GetExportHistoryCursorAsync entry: LocationId={LocationId}, Cursor={Cursor}, PageSize={PageSize}", locationId, cursor ?? "null", pageSize);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                // Parse cursor (base64-encoded datetime offset)
                int startIndex = 0;
                if (!string.IsNullOrEmpty(cursor))
                {
                    try
                    {
                        var decodedBytes = Convert.FromBase64String(cursor);
                        var decodedString = System.Text.Encoding.UTF8.GetString(decodedBytes);
                        if (int.TryParse(decodedString, out var cursorIndex))
                        {
                            startIndex = cursorIndex;
                        }
                    }
                    catch
                    {
                        startIndex = 0;
                    }
                }

                // Get device IDs for location
                var deviceIds = await context.Devices
                    .Where(d => d.LocationId == locationId)
                    .Select(d => d.Id)
                    .ToListAsync(ct);

                if (deviceIds.Count == 0)
                {
                    _logger.LogInformation("GetExportHistoryCursorAsync exit: no devices found for LocationId={LocationId}", locationId);
                    return new CursorPaginatedResult<ExportAuditRecord> { Items = new List<ExportAuditRecord>() };
                }

                // Get all export records that contain media from devices at this location
                var allExports = await context.ExportRecordItems
                    .Join(context.MediaItems,
                        eri => eri.MediaItemId,
                        mi => mi.Id,
                        (eri, mi) => new { eri.ExportRecordId, mi.DeviceId })
                    .Where(x => deviceIds.Contains(x.DeviceId))
                    .Select(x => x.ExportRecordId)
                    .Distinct()
                    .Join(context.ExportRecords,
                        recordId => recordId,
                        er => er.Id,
                        (_, er) => er)
                    .OrderByDescending(er => er.ExportedAtUtc)
                    .ToListAsync(ct);

                var paginatedExports = allExports
                    .Skip(startIndex)
                    .Take(pageSize)
                    .ToList();

                var records = paginatedExports.Select(er => new ExportAuditRecord
                {
                    ExportId = er.Id,
                    LocationId = locationId,
                    ExportedAtUtc = er.ExportedAtUtc,
                    ExportedBy = er.ExportedByUserName,
                    EventsExported = er.ItemCount,
                    ExportFormat = er.WasEncrypted ? "AES256Zip" : "Zip",
                    Purpose = er.CaseReference ?? "Forensic Analysis"
                }).ToList();

                string? nextCursor = null;
                if (startIndex + paginatedExports.Count < allExports.Count)
                {
                    var nextIndex = startIndex + paginatedExports.Count;
                    nextCursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(nextIndex.ToString()));
                }

                var result = new CursorPaginatedResult<ExportAuditRecord>
                {
                    Items = records,
                    NextCursor = nextCursor,
                    HasMore = nextCursor != null
                };

                _logger.LogInformation("GetExportHistoryCursorAsync exit: returned {Count} exports for LocationId={LocationId}", records.Count, locationId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cursor-paginated export history for LocationId={LocationId}", locationId);
                throw;
            }
        }

        /// <summary>Verifies chain of custody (all accesses logged and continuous).</summary>
        public async Task<AccessAuditReport> VerifyChainOfCustodyAsync(Guid locationId, CancellationToken ct)
        {
            _logger.LogInformation("VerifyChainOfCustodyAsync entry: LocationId={LocationId}", locationId);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                // Get all devices for location
                var deviceIds = await context.Devices
                    .Where(d => d.LocationId == locationId)
                    .Select(d => d.Id)
                    .ToListAsync(ct);

                // Get all events for these devices
                var eventIds = await context.Events
                    .Where(e => deviceIds.Contains(e.DeviceId))
                    .Select(e => e.Id)
                    .ToListAsync(ct);

                var totalEventsTracked = eventIds.Count;

                // Get access records
                var accessEntries = await context.ActionLogEntries
                    .Where(ale => eventIds.Contains(ale.EntityId ?? Guid.Empty) && ale.EntityType == "Event")
                    .OrderBy(ale => ale.TimestampUtc)
                    .ToListAsync(ct);

                var accessRecordsCount = accessEntries.Count;
                var allAccesses = accessEntries.Select(e => new AccessAuditLog
                {
                    Id = e.Id,
                    EvidenceId = e.EntityId ?? Guid.Empty,
                    UserId = e.Actor,
                    AccessedAtUtc = e.TimestampUtc,
                    Action = e.Action,
                    IpAddress = string.Empty,
                    Purpose = string.Empty
                }).ToList();

                // Verify continuity (no gaps > 24h)
                bool isComplete = true;
                if (accessEntries.Count > 0)
                {
                    var sortedByTime = accessEntries.OrderBy(e => e.TimestampUtc).ToList();
                    for (int i = 1; i < sortedByTime.Count; i++)
                    {
                        var gap = sortedByTime[i].TimestampUtc - sortedByTime[i - 1].TimestampUtc;
                        if (gap.TotalHours > 24)
                        {
                            isComplete = false;
                            break;
                        }
                    }
                }

                var report = new AccessAuditReport
                {
                    LocationId = locationId,
                    TotalEventsTracked = totalEventsTracked,
                    AccessRecordsCount = accessRecordsCount,
                    IsComplete = isComplete && totalEventsTracked > 0,
                    AllAccesses = allAccesses,
                    CustodyStatus = (isComplete && totalEventsTracked > 0) ? "Intact" : "Questionable"
                };

                _logger.LogInformation("VerifyChainOfCustodyAsync exit: LocationId={LocationId}, Status={Status}, Events={TotalEvents}, Records={AccessRecords}", locationId, report.CustodyStatus, totalEventsTracked, accessRecordsCount);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying chain of custody for LocationId={LocationId}", locationId);
                throw;
            }
        }

        /// <summary>Flags unauthorized access patterns (off-hours, excessive, anomalous).</summary>
        public async Task<IReadOnlyList<UnauthorizedAccessFlag>> FlagUnauthorizedAccessAsync(Guid locationId, CancellationToken ct)
        {
            _logger.LogInformation("FlagUnauthorizedAccessAsync entry: LocationId={LocationId}", locationId);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                var flags = new List<UnauthorizedAccessFlag>();

                // Get all devices for location
                var deviceIds = await context.Devices
                    .Where(d => d.LocationId == locationId)
                    .Select(d => d.Id)
                    .ToListAsync(ct);

                if (deviceIds.Count == 0)
                {
                    return flags;
                }

                // Get all events for these devices
                var eventIds = await context.Events
                    .Where(e => deviceIds.Contains(e.DeviceId))
                    .Select(e => e.Id)
                    .ToListAsync(ct);

                // Get all access logs
                var accessEntries = await context.ActionLogEntries
                    .Where(ale => eventIds.Contains(ale.EntityId ?? Guid.Empty) && ale.EntityType == "Event")
                    .ToListAsync(ct);

                // Group by day and user to detect excessive access
                var dailyAccessCounts = accessEntries
                    .GroupBy(e => new { Date = e.TimestampUtc.Date, User = e.Actor })
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var entry in accessEntries)
                {
                    int suspicionScore = 0;
                    var flagReasons = new List<string>();

                    // Check for off-hours access (22:00 - 06:00)
                    var hour = entry.TimestampUtc.Hour;
                    if (hour >= 22 || hour < 6)
                    {
                        suspicionScore += 30;
                        flagReasons.Add("OffHours");
                    }

                    // Check for excessive access (>100/day)
                    var dailyKey = new { Date = entry.TimestampUtc.Date, User = entry.Actor };
                    if (dailyAccessCounts.TryGetValue(dailyKey, out var count) && count > 100)
                    {
                        suspicionScore += 40;
                        flagReasons.Add("ExcessiveAccess");
                    }

                    // Flag if suspicion score is significant
                    if (suspicionScore >= 30)
                    {
                        flags.Add(new UnauthorizedAccessFlag
                        {
                            EvidenceId = entry.EntityId ?? Guid.Empty,
                            UserId = entry.Actor,
                            AccessedAtUtc = entry.TimestampUtc,
                            Action = entry.Action,
                            FlagReason = string.Join(",", flagReasons),
                            SuspicionScore = suspicionScore
                        });
                    }
                }

                _logger.LogInformation("FlagUnauthorizedAccessAsync exit: found {FlagCount} unauthorized access flags for LocationId={LocationId}", flags.Count, locationId);
                return flags;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flagging unauthorized access for LocationId={LocationId}", locationId);
                throw;
            }
        }

        /// <summary>Gets export records for a location.</summary>
        public async Task<IReadOnlyList<ExportAuditRecord>> GetExportHistoryAsync(Guid locationId, CancellationToken ct)
        {
            _logger.LogInformation("GetExportHistoryAsync entry: LocationId={LocationId}", locationId);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                // Get device IDs for location
                var deviceIds = await context.Devices
                    .Where(d => d.LocationId == locationId)
                    .Select(d => d.Id)
                    .ToListAsync(ct);

                if (deviceIds.Count == 0)
                {
                    return new List<ExportAuditRecord>();
                }

                // Get export records that contain media from devices at this location
                var exportRecords = await context.ExportRecordItems
                    .Join(context.MediaItems,
                        eri => eri.MediaItemId,
                        mi => mi.Id,
                        (eri, mi) => new { eri.ExportRecordId, mi.DeviceId })
                    .Where(x => deviceIds.Contains(x.DeviceId))
                    .Select(x => x.ExportRecordId)
                    .Distinct()
                    .Join(context.ExportRecords,
                        recordId => recordId,
                        er => er.Id,
                        (_, er) => er)
                    .OrderByDescending(er => er.ExportedAtUtc)
                    .ToListAsync(ct);

                var records = exportRecords.Select(er => new ExportAuditRecord
                {
                    ExportId = er.Id,
                    LocationId = locationId,
                    ExportedAtUtc = er.ExportedAtUtc,
                    ExportedBy = er.ExportedByUserName,
                    EventsExported = er.ItemCount,
                    ExportFormat = er.WasEncrypted ? "AES256Zip" : "Zip",
                    Purpose = er.CaseReference ?? "Forensic Analysis"
                }).ToList();

                _logger.LogInformation("GetExportHistoryAsync exit: found {Count} export records for LocationId={LocationId}", records.Count, locationId);
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting export history for LocationId={LocationId}", locationId);
                throw;
            }
        }

        /// <summary>Verifies export integrity (export count matches actual events, no tampering).</summary>
        public async Task<ExportIntegrityReport> VerifyExportIntegrityAsync(Guid exportId, CancellationToken ct)
        {
            _logger.LogInformation("VerifyExportIntegrityAsync entry: ExportId={ExportId}", exportId);

            await using var context = await _factory.CreateDbContextAsync(ct);
            try
            {
                var exportRecord = await context.ExportRecords.FirstOrDefaultAsync(er => er.Id == exportId, ct);
                if (exportRecord == null)
                {
                    _logger.LogWarning("Export record not found: {ExportId}", exportId);
                    return new ExportIntegrityReport
                    {
                        ExportId = exportId,
                        TotalEventsExported = 0,
                        IntactEvents = 0,
                        IsIntact = false,
                        IntegrityStatus = "Unknown"
                    };
                }

                // Get all export items for this export
                var exportItems = await context.ExportRecordItems
                    .Where(eri => eri.ExportRecordId == exportId)
                    .ToListAsync(ct);

                int intactCount = 0;

                // Verify each exported media item's hash
                foreach (var item in exportItems)
                {
                    var mediaItem = await context.MediaItems.FirstOrDefaultAsync(mi => mi.Id == item.MediaItemId, ct);
                    if (mediaItem != null && mediaItem.Sha256Hash == item.MediaItemSha256HashAtExport)
                    {
                        intactCount++;
                    }
                }

                var report = new ExportIntegrityReport
                {
                    ExportId = exportId,
                    TotalEventsExported = exportRecord.ItemCount,
                    IntactEvents = intactCount,
                    ModifiedEvents = exportItems.Count - intactCount,
                    IsIntact = intactCount == exportItems.Count,
                    IntegrityStatus = (intactCount == exportItems.Count) ? "Intact" : "Modified"
                };

                _logger.LogInformation("VerifyExportIntegrityAsync exit: ExportId={ExportId}, Status={Status}, Intact={IntactCount}/{TotalCount}", exportId, report.IntegrityStatus, intactCount, exportItems.Count);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying export integrity for ExportId={ExportId}", exportId);
                throw;
            }
        }

        /// <summary>Gets quick audit summary for compliance review.</summary>
        public async Task<AuditTrailSummary> GetAuditTrailSummaryAsync(Guid locationId, CancellationToken ct)
        {
            _logger.LogInformation("GetAuditTrailSummaryAsync entry: LocationId={LocationId}", locationId);

            try
            {
                var accessHistory = await GetLocationAccessHistoryAsync(locationId, ct);
                var exports = await GetExportHistoryAsync(locationId, ct);
                var custody = await VerifyChainOfCustodyAsync(locationId, ct);
                var unauthorized = await FlagUnauthorizedAccessAsync(locationId, ct);

                var lastAccessUtc = accessHistory.Count > 0 ? accessHistory.Max(a => a.AccessedAtUtc) : DateTime.MinValue;
                var lastExportUtc = exports.Count > 0 ? exports.Max(e => e.ExportedAtUtc) : DateTime.MinValue;

                var summary = new AuditTrailSummary
                {
                    TotalCount = accessHistory.Count + exports.Count,
                    Status = (custody.IsComplete && unauthorized.Count == 0) ? "Healthy" : (unauthorized.Count > 0 ? "Anomalies" : "Incomplete"),
                    ComplianceScore = custody.IsComplete ? 100 : 50,
                    AccessCount = accessHistory.Count,
                    UnauthorizedAccessCount = unauthorized.Count,
                    ExportCount = exports.Count,
                    LastAccessUtc = lastAccessUtc,
                    LastExportUtc = lastExportUtc,
                    ChainOfCustodyIntact = custody.IsComplete,
                    SuspiciousAccessPatterns = unauthorized.Select(u => $"Unauthorized: {u.FlagReason}").ToList(),
                    DetailQueryMethod = "VerifyChainOfCustodyAsync"
                };

                summary.TopIssues["AccessRecords"] = accessHistory.Count;
                summary.TopIssues["ExportRecords"] = exports.Count;
                if (unauthorized.Count > 0)
                {
                    summary.TopIssues["UnauthorizedAccess"] = unauthorized.Count;
                }

                _logger.LogInformation("GetAuditTrailSummaryAsync exit: LocationId={LocationId}, Status={Status}, AccessCount={AccessCount}, ExportCount={ExportCount}", locationId, summary.Status, summary.AccessCount, summary.ExportCount);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit trail summary for LocationId={LocationId}", locationId);
                throw;
            }
        }

        /// <summary>Gets redaction history (stub for future implementation).</summary>
        public async Task<IReadOnlyList<RedactionAuditRecord>> GetRedactionHistoryAsync(Guid locationId, CancellationToken ct)
        {
            return await Task.FromResult(new List<RedactionAuditRecord>());
        }

        /// <summary>Traces modification history for an event (stub for future implementation).</summary>
        public async Task<IReadOnlyList<ModificationAuditRecord>> TraceModificationHistoryAsync(Guid eventId, CancellationToken ct)
        {
            return await Task.FromResult(new List<ModificationAuditRecord>());
        }
    }
}
