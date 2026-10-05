# Backward Compatibility: Version & Update-Check APIs

This document outlines the backward compatibility promises for all version and update-check related changes during Phase C (Weeks 1-2).

## Executive Summary

**Existing clients are unaffected.** All new endpoints are additions, not replacements. Existing `/api/v1/update-check` consumers continue to work without modification. Version bumping and release notes automation are internal CI/CD concerns invisible to clients.

---

## Existing Endpoints (Unchanged)

### `GET /api/v1/update-check`

- **Status:** Unchanged from before Phase C
- **Auth:** Requires SuperAdmin role + Local network tier
- **Response Format:** `UpdateCheckStateDto` (unchanged schema)
- **Behavior:** Returns current update check state with version availability info
- **Backward Compatibility:** Fully preserved — no schema changes, no behavior changes
- **Client Impact:** Zero — clients relying on this endpoint continue to work exactly as before

### `POST /api/v1/update-check/check-now`

- **Status:** Unchanged from before Phase C
- **Auth:** Requires SuperAdmin role + Local network tier
- **Response Format:** `UpdateCheckStateDto` (unchanged schema)
- **Behavior:** Triggers immediate update check and returns refreshed state
- **Backward Compatibility:** Fully preserved
- **Client Impact:** Zero

---

## New Endpoints (Additions)

### `GET /api/v1/system/version` — **NEW** (Phase C Week 1)

- **Status:** Public (no authentication required)
- **Response:** `SystemVersionDto`
  ```json
  {
    "version": "1.0.0-dev.1",
    "buildDate": "2024-10-05T12:00:00Z",
    "channel": "dev"
  }
  ```
- **Purpose:** Provides current system version, build timestamp, and release channel
- **Safety:** Clients that ignore this endpoint are unaffected; it's purely additive
- **Use Case:** Clients need version info before pairing; used by update-check logic

### `GET /api/v1/system/version-manifest` — **NEW** (Phase C Week 1)

- **Status:** Public (no authentication required)
- **Response:** `VersionManifestDto`
  ```json
  {
    "currentVersion": { "version": "1.0.0-dev.1", "buildDate": "2024-10-05T12:00:00Z", "channel": "dev" },
    "latestAvailable": null,
    "downloadUrl": null,
    "changelogUrl": null,
    "updateAvailable": false
  }
  ```
- **Purpose:** Integrates version and update-check information for clients
- **Note (Week 1):** `latestAvailable`, `downloadUrl`, `changelogUrl`, and `updateAvailable` are all placeholder values; integration with update-check service happens in Week 2
- **Safety:** Clients that ignore this endpoint are unaffected
- **Future (Week 2):** Will integrate with update-check service to populate update availability

---

## Scripts & Automation (Internal)

### `.github/scripts/bump-version.sh` — **NEW** (Phase C Week 2 Days 1-2)

- **Type:** CI/CD automation script
- **Scope:** Internal only — no client visibility
- **Function:** Auto-increments `version.json` on `dev` and `testing` branches after commits
- **Client Impact:** None — version bumping happens in repository, not in deployed binaries
- **Use Case:** Keeps dev/testing versions current without manual updates

### `.github/scripts/generate-release-notes.sh` — **NEW** (Phase C Week 2 Days 3-4)

- **Type:** CI/CD automation script
- **Scope:** Internal only — generates GitHub release descriptions
- **Function:** Parses conventional commits and generates markdown release notes
- **Client Impact:** None — used for GitHub releases, not client-facing
- **Use Case:** Automates release documentation on tag push

### `.github/scripts/generate-version-manifest.sh` — **NEW** (Phase C Week 2 Days 3-4)

- **Type:** CI/CD automation script
- **Scope:** Internal only — generates JSON manifest
- **Function:** Creates `version-manifest.json` from `version.json` and other sources
- **Client Impact:** None (Week 1) — manifest is served via API; script is internal
- **Future (Week 2):** Will be consumed by API to populate update-check responses

---

## Migration Path

### For Existing Clients

1. **No action required.** Existing `/api/v1/update-check` clients continue working
2. **Optional:** Adopt `/api/v1/system/version` for version queries before pairing
3. **Optional:** Switch to `/api/v1/system/version-manifest` for integrated version/update data (after Week 2 update-check integration)

### For New Clients

1. Call `/api/v1/system/version` to get current version before pairing
2. (Week 2+) Call `/api/v1/system/version-manifest` to check for updates
3. (Week 2+) Existing `/api/v1/update-check` still works for deeper control

---

## Testing Checklist

### Local Testing

- [x] Existing `/api/v1/update-check` still works (no schema changes)
- [x] New `/api/v1/system/version` returns valid `SystemVersionDto`
- [x] New `/api/v1/system/version-manifest` returns valid `VersionManifestDto`
- [x] Version bumping (bump-version.sh) doesn't break existing builds
- [x] Release notes generation (generate-release-notes.sh) produces valid markdown
- [x] Version manifest generation (generate-version-manifest.sh) produces valid JSON

### Integration Testing

- [ ] Deploy to staging
- [ ] Call `/api/v1/system/version` → verify response contains version, build date, channel
- [ ] Call `/api/v1/update-check` → verify response is unchanged from before
- [ ] Call `/api/v1/system/version-manifest` → verify response integrates both
- [ ] Trigger a dev build → verify `version.json` auto-increments
- [ ] Tag a release → verify release notes are generated and GitHub release is created
- [ ] Verify old clients still work (no breaking changes)

### Regression Testing

- [ ] Existing update-check clients (SuperAdmin only) still authenticate correctly
- [ ] Public endpoints (`/version`, `/version-manifest`) require no auth
- [ ] Version data consistency across all endpoints
- [ ] Backward compatibility: old code still compiles/runs with new assemblies

---

## Known Limitations & Future Work

### Week 1 (Current)

- `VersionManifestDto.LatestAvailable` is always `null`
- `VersionManifestDto.UpdateAvailable` is always `false`
- `VersionManifestDto.DownloadUrl` is always `null`
- `VersionManifestDto.ChangelogUrl` is always `null`
- Update-check integration deferred to Week 2

### Week 2

- [x] Integrate `/api/v1/update-check` with manifest endpoints
- [x] Populate `LatestAvailable`, `UpdateAvailable`, download/changelog URLs
- [x] Auto-bump versions on dev/testing branches
- [x] Auto-generate release notes on tags
- [x] End-to-end integration tests (this file)
- [ ] Manual QA verification (see testing checklist above)

---

## Deployment Notes

### No Breaking Changes

- Database schema: Unchanged (version data is computed/cached, not stored)
- Configuration: No new required config (version sources from code/env)
- Deployment: Standard CI/CD flow (no special steps)

### Rollback

If issues arise:
1. Revert commit that added new endpoints
2. Existing `/api/v1/update-check` continues unaffected
3. Clients using old endpoints recover automatically

---

## Contact & Questions

For backward compatibility concerns or client questions, refer to this document. For implementation details, see the test files in `VideoForensics.WebApp.Tests/`:
- `SystemVersionEndpointsTests.cs` — unit tests for version endpoints
- `UpdateCheckEndToEndTests.cs` — integration tests for full flow
