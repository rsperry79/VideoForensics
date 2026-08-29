# Claude Desktop Configuration for VideoForensics MCP Server

## Overview

The VideoForensics MCP Server exposes a comprehensive 4-phase forensic query system to Claude Desktop, enabling AI-assisted forensic analysis via tool integration. The server implements four interconnected analysis phases:

1. **Phase 1: Timeline & Patterns** - Event distribution, gaps, and coordinated anomalies
2. **Phase 2: Evidence Integrity** - Tampering detection, hash verification, and download completeness
3. **Phase 3: Correlation Queries** - Device health correlation with event/gap anomalies
4. **Phase 4: Access & Export Audit** - Chain of custody and evidence access tracking

Plus **Bonus: Jamming Analysis** - RF interference detection from RSSI patterns.

## Installation Steps

### 1. Build the MCP Server

```bash
dotnet publish -c Release src/client/VideoForensics.Mcp/VideoForensics.Mcp.csproj
```

### 2. Locate the Published Executable

After building, the executable will be at:

```
src/client/VideoForensics.Mcp/bin/Release/net10.0/publish/VideoForensics.Mcp.exe
```

**Note:** You will need the full absolute path for the Claude Desktop configuration file.

## Claude Desktop Configuration

### Configuration File Location

Claude Desktop reads MCP server configuration from:

```
%AppData%/Claude/claude_desktop_config.json
```

On Windows, this typically expands to:

```
C:\Users\<YourUsername>\AppData\Roaming\Claude\claude_desktop_config.json
```

### Configuration Format

Add the VideoForensics MCP server to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "videoforensics": {
      "command": "C:\\full\\path\\to\\VideoForensics.Mcp.exe"
    }
  }
}
```

### Example with Real Path

If your user is `rsperry79` and you cloned to `C:\Users\richa\source\repos\VideoForensics`, your config would be:

```json
{
  "mcpServers": {
    "videoforensics": {
      "command": "C:\\Users\\richa\\source\\repos\\VideoForensics\\src\\client\\VideoForensics.Mcp\\bin\\Release\\net10.0\\publish\\VideoForensics.Mcp.exe"
    }
  }
}
```

## Startup & Lifecycle

- **Restart Required:** After updating `claude_desktop_config.json`, restart Claude Desktop completely
- **Auto-Start:** The MCP server automatically starts when Claude Desktop launches
- **Process Model:** Runs as a stdio subprocess (stdin/stdout for MCP protocol communication)
- **Shared State:** Uses the same database and authentication as the console VideoForensics client (same `%AppData%/VideoForensics` directory)

## Available Tools by Phase

### Phase 1: Timeline & Patterns (TimelineTools - 8 methods)

Tools for analyzing event distribution, gaps, and temporal anomalies:

- **GetTimelineSummary** - Quick health summary for a date range (event count, gap count, peak hours)
- **GetRecordingGapsPaginated** - Offset-based pagination for recording gaps with detailed metadata
- **GetRecordingGapsCursor** - Cursor-based pagination for streaming large gap datasets
- **GetEventCountByHour** - Hourly distribution of events for time-of-day analysis
- **GetEventCountByDay** - Daily totals for trend analysis
- **GetPeakActivityPeriods** - Top activity hours ranked by event density
- **VerifyTimelineIntegrity** - Coverage % and integrity report for entire timeline
- **FindSuspiciousCoordinatedActivity** - Detect multi-device anomalies (synchronized gaps/events)

### Phase 2: Evidence Integrity (IntegrityTools - 8 methods)

Tools for detecting tampering, verifying completeness, and validating evidence:

- **GetIntegritySummary** - Quick integrity health check (tampering flags, score, download status)
- **GetTamperingIndicatorsPaginated** - Tampering flags ranked by confidence score with pagination
- **GetDownloadHistoryCursor** - Streaming download audit records (cursor-based)
- **ComputeEventIntegrityScore** - Compute 0-100% integrity score for event set
- **VerifyDownloadCompleteness** - Completeness report (missing segments, retry status)
- **GetTamperingIndicators** - All tampering flags ranked by confidence
- **VerifyEventHashes** - Hash-based tampering detection (compare stored vs computed hashes)
- **GetRecordingGapsPaginated** - Gap detection with integrity context

### Phase 3: Correlation Queries (CorrelationTools - 6 methods)

Tools for correlating device health issues with event/gap anomalies:

- **GetCorrelationSummary** - Sync health overview (offline periods, signal degradation, battery issues)
- **GetHealthRelatedGapsPaginated** - Gaps correlated with device health issues (pagination)
- **GetEventHealthCorrelationCursor** - Events/gaps paired with device health status (cursor streaming)
- **AnalyzeSyncHealth** - Per-device uptime analysis and sync pattern report
- **IdentifyHealthRelatedGaps** - Find gaps caused by low battery, signal loss, or offline periods
- **AnalyzeDeviceReliability** - Uptime % vs capture rate correlation

### Phase 4: Access & Export Audit (AuditTrailTools - 7 methods)

Tools for chain-of-custody tracking and export audit:

- **GetAuditTrailSummary** - Chain of custody overview (who accessed, when, export count)
- **GetAccessHistoryPaginated** - Evidence access history with timestamp and user (offset pagination)
- **GetExportHistoryCursor** - Streaming export records (cursor-based for large exports)
- **VerifyChainOfCustody** - Custody integrity report (breaks in chain, unauthorized access flags)
- **FlagUnauthorizedAccess** - Detect off-hours or excessive access patterns
- **GetExportHistory** - All exports for location with metadata
- **VerifyExportIntegrity** - Export tampering detection (verify export package integrity)

### Bonus: Jamming Analysis (JammingTools - 4 methods)

Tools for detecting RF interference patterns:

- **RunJammingDetection** - Detect RF interference from RSSI signal strength patterns
- **RecordJammingIncident** - Manual incident logging with timestamp and severity
- **GetJammingStats** - Summary statistics by device (incident count, avg severity)
- **GetJammingIncidents** - Raw incident records with full details

## Resources

- **jamming-analysis-instructions** - Markdown playbook for RF interference analysis workflow and interpretation

## Usage Patterns

### Fast Decision Point (Use Summaries First)

For quick assessment of a location's forensic status, call all 4 summary methods in parallel:

```
GetTimelineSummary(location="Front Door", dateRange="2025-01-01..2025-01-31")
GetIntegritySummary(location="Front Door")
GetCorrelationSummary(location="Front Door")
GetAuditTrailSummary(location="Front Door")
```

**Performance:** All 4 in parallel ~100ms instead of sequential 400ms. Provides:
- Overall event/gap picture
- Tampering risk score
- Device health correlation
- Access audit status

### Detailed Analysis (If Summary Shows Anomalies)

- **Timeline anomalies detected** → Call `GetRecordingGaps` and `FindSuspiciousCoordinatedActivity`
- **Integrity issues detected** → Call `GetTamperingIndicators` and `VerifyDownloadCompleteness`
- **Health issues detected** → Call `AnalyzeSyncHealth` and `IdentifyHealthRelatedGaps`
- **Chain of custody concerns** → Call `VerifyChainOfCustody` and `FlagUnauthorizedAccess`

### Streaming Large Datasets

For results exceeding 1000 items, use cursor-based pagination:

```
GetRecordingGapsCursor(location="Front Door", pageSize=1000, cursor=null)
# Continue with NextCursor from response until cursor is null
GetRecordingGapsCursor(location="Front Door", pageSize=1000, cursor=<NextCursor>)
```

No blocking; fully concurrent streaming suitable for Claude's parallel tool invocation.

## Shared State

The VideoForensics MCP Server shares state with the console client:

- **Same Database:** Both read/write from the same forensic database
- **Same Directory:** `%AppData%/VideoForensics` directory used by both
- **Same Credentials:** Authentication tokens and provider sessions are shared
- **Consistent Data:** Forensic queries reflect events, devices, and downloads populated by either client

This means you can populate the database with the console client and analyze with Claude Desktop, or vice versa.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Server not appearing in Claude Desktop | Verify the executable path in `claude_desktop_config.json` is correct and absolute. Restart Claude Desktop completely. |
| Tool call failures / method not found | Ensure the VideoForensics database is initialized. Run the console client at least once to set up the database schema. |
| Permissions errors when accessing database | The exe may need to run as administrator depending on `%AppData%` ACLs. Retry with elevated privileges. |
| Server crashes on startup | Check the Claude Desktop logs in `%AppData%/Claude/logs/` for error details. Verify database connectivity. |

## Performance Notes

Typical latencies for forensic queries:

- **Summary methods:** <50ms (cached health overview)
- **Paginated queries:** <100ms per page (offset-based, 1000 items per page)
- **Cursor pagination:** <200ms per chunk (streaming large datasets, fully incremental)
- **Parallel queries:** No blocking with Task.WhenAll; all concurrent via stdio multiplexing

For 10,000+ gap records, use cursor pagination instead of offset pagination to avoid scanning overhead.

---

**Last Updated:** 2026-08-28  
**Supported Versions:** .NET 10.0+  
**Claude Desktop Requirement:** Latest version recommended
