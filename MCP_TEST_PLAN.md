# VideoForensics MCP Server Testing Plan

## Overview
The VideoForensics MCP (Model Context Protocol) server exposes 4 phases of forensic analysis tools:
- Phase 1: Timeline & Patterns (8 methods)
- Phase 2: Evidence Integrity (10 methods)
- Phase 3: Correlation Queries (7 methods)
- Phase 4: Access & Export Audit (9 methods)

## Phase 0: Server Initialization

**Tests:**
- [ ] Server starts without fatal errors
- [ ] Database initializes (lazy-loaded)
- [ ] All 4 tool classes register (Timeline, Integrity, Correlation, Audit)
- [ ] jamming-analysis-instructions resource available
- [ ] Configuration loads from database
- [ ] Events backfill completes (one-time)
- [ ] Stdio transport ready for Claude Desktop
- [ ] All DI services resolve correctly
- [ ] Initialization log includes optimization details

**Success:** "MCP SERVER READY FOR CONNECTIONS" logged

---

## Phase 1: Timeline & Patterns

| Tool | Test Case | Validation |
|------|-----------|-----------|
| GetTimelineSummary | Empty DB | Returns valid summary |
| GetTimelineSummary | With events | Event counts accurate |
| GetRecordingGapsPaginated | Pagination | Offset works, no duplicates |
| GetRecordingGapsCursor | Cursor pagination | Valid cursor progression |
| GetEventCountByHour | Hourly distribution | Keys 0-23, sums match |
| GetEventCountByDay | Daily distribution | Dates yyyy-MM-dd, counts cumulative |
| GetPeakActivityPeriods | Top hours | Sorted, respects limit |
| VerifyTimelineIntegrity | Sequence validation | No reversions, timestamps valid |
| GetCoordinatedEvents | Event clustering | Within time window tolerance |
| FindSuspiciousCoordinatedActivity | Pattern detection | Flags with severity |

---

## Phase 2: Evidence Integrity

| Tool | Test Case | Validation |
|------|-----------|-----------|
| GetIntegritySummary | Aggregate report | Counts match detailed phase |
| GetTamperingIndicatorsPaginated | Detection | Hash mismatches, missing files, inconsistencies |
| GetDownloadHistoryCursor | Cursor audit trail | Order preserved, timestamps included |
| ComputeEventIntegrityScore | Score range | 0-100, reflects all issues |
| VerifyDownloadCompleteness | Gap detection | Expected vs actual, retry strategy |
| VerifyEventHashes | Re-hash validation | SHA256 matches stored |
| GetDownloadHistoryCursor | Cursor integrity | No duplicates across pages |

**Validation:** Hash verification works end-to-end

---

## Phase 3: Correlation Queries

| Tool | Test Case | Validation |
|------|-----------|-----------|
| GetCorrelationSummary | Cross-device | Aggregates correctly, < 50KB |
| GetHealthRelatedGapsPaginated | Health correlation | Battery, connectivity filters work |
| GetEventHealthCorrelationCursor | Event-health link | Cursor pagination, metrics included |
| AnalyzeSyncHealth | Sync analysis | Success rates, recovery suggestions |
| IdentifyHealthRelatedGaps | Gap analysis | Root cause identification |
| AnalyzeDeviceReliability | Reliability scoring | Uptime %, MTBF calculated |

**Validation:** Multi-device analysis consistent

---

## Phase 4: Access & Export Audit

| Tool | Test Case | Validation |
|------|-----------|-----------|
| GetAuditTrailSummary | Summary stats | Rapid response (< 100ms) |
| GetAccessHistoryPaginated | Access logs | User/IP/timestamp included |
| GetExportHistoryCursor | Export audit | File list with hashes |
| VerifyChainOfCustody | Custody chain | Unbroken access history |
| FlagUnauthorizedAccess | Access detection | Flags with severity levels |
| GetExportHistory | Export record | Complete metadata |
| VerifyExportIntegrity | Export validation | Files verified, hashes checked |

**Validation:** Chain of custody unbroken

---

## Error Handling

**Invalid Input:**
- [ ] Null device ID → error
- [ ] End date < start date → error
- [ ] Negative pagination offset → error
- [ ] Page size = 0 → error
- [ ] Invalid cursor → error

**System Resilience:**
- [ ] Database unavailable → graceful degradation
- [ ] Concurrent requests → no data corruption
- [ ] Large result sets (1M+ records) → no timeout
- [ ] Memory efficient cursor pagination

---

## Integration Tests

**Parallel Execution:**
```csharp
await Task.WhenAll(
    timelineRepo.GetTimelineSummaryAsync(...),
    integrityRepo.GetIntegritySummaryAsync(...),
    correlationRepo.GetCorrelationSummaryAsync(...),
    auditRepo.GetAuditTrailSummaryAsync(...)
);
```
- [ ] All 4 queries complete without blocking
- [ ] No race conditions
- [ ] Total time < sum of individual times

**Data Consistency:**
- [ ] Event counts match across all phases
- [ ] Download counts consistent
- [ ] Hash mismatches reported everywhere
- [ ] Timestamps aligned

---

## Performance Baselines

| Operation | Target | Measured |
|-----------|--------|----------|
| Summary queries | < 100ms | ? |
| Paginated queries | < 500ms per page | ? |
| Cursor pagination | < 300ms per page | ? |
| Concurrent 4-phase | < 2s total | ? |
| Large dataset (1M events) | < 5s | ? |

---

## Test Execution

**Smoke Test (5 min):**
```bash
# Terminal 1: Start server
dotnet run --project src/client/VideoForensics.Mcp

# Terminal 2: Test basic calls
curl http://localhost:3000/timeline/summary
curl http://localhost:3000/integrity/summary
```

**Quick MCP Verification:**
- [ ] Server responds to capability requests
- [ ] All 34 methods callable
- [ ] No startup errors
- [ ] Logs show initialization sequence

---

## Known Issues to Fix

| Issue | Status | Fix |
|-------|--------|-----|
| (Add after testing) | | |

