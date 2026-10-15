# CreateNetworkShares user-management TODO

**Status on origin/dev (checked 2026-10-09)**: STILL OPEN. `TODO(user-management)` remains at `deploy/windows/VideoForensics.iss` line 486; no commit on `origin/dev` has touched the .iss since #91.

**Source**: `plans/archive/external-auth-smb-integration.md` line 246 (critical-files list: `VideoForensics.iss` `CreateNetworkShares`, `TODO(user-management)`).

## Verdict

STILL OPEN, and it is a design decision plus a follow-up feature, not an installer bug. `CreateNetworkShares()` creates the `VideoForensicsAdmin` and `VideoForensicsSuperUser` local groups, adds only the account running Setup to SuperUser, and sets NTFS and SMB share ACLs (Modify for both groups, Read for `Users`). Nothing else in the repo manages membership of those groups: `git grep` on origin/dev for `localgroup` / `VideoForensicsSuperUser` / `Add-LocalGroupMember` in `.cs`, `.ps1` and `.razor` finds nothing. The external-auth SMB work only listed the .iss as a reference and did not address the TODO. Today the workaround is manual (Computer Management or `net localgroup VideoForensicsSuperUser <name> /add`), which the task description in the .iss already points to.

## Scope

In scope: decide where Windows-account membership of the two share groups is managed, then implement the chosen surface. The TODO itself says an installer-time UI is the wrong fix because membership changes over the life of an install.

Recommended options, in order:
1. Minimal (S): keep manual membership, replace the TODO with a pointer to a documented procedure, and ship a small Tools script (`Add-VideoForensicsShareUser.ps1`) wrapping `Add-LocalGroupMember` / `Remove-LocalGroupMember` with validation. No server code.
2. Full (L): an in-app SuperAdmin screen (WebApp host, Windows-only, behind the existing SuperAdmin policy) that lists, adds and removes members of the two groups through a Windows-only service behind an interface in a `Contracts/` folder. Must respect the client/server rules in CLAUDE.md (DTOs in `VideoForensics.Api.Contracts`, `/api/v1/...` route, localized UI strings, `ToDto()` mapping).

Out of scope: changing share or NTFS ACL model, installer-time user pickers, domain or Entra accounts.

## Code and file touchpoints

| file | change | why |
|---|---|---|
| `deploy/windows/VideoForensics.iss` | Replace `TODO(user-management)` comment (line ~486) with a reference to the chosen mechanism; update the `networkshare` task description (line 137) if the wording changes | Verified present on origin/dev; this is where the TODO lives |
| `deploy/windows/VideoForensics.iss` | No logic change expected for option 1; for option 2 none either (membership is runtime, not install-time) | `CreateNetworkShares` and `GrantSharingAcls` already create groups and ACLs idempotently |
| New: Tools script, location to confirm against the existing Tools layout in the .iss `[Files]` section (option 1) | `Add-VideoForensicsShareUser.ps1` plus a [Files] entry | Documented, validated membership management |
| New (option 2): Windows-only group service plus contract, API endpoint, DTOs, Razor page, `.resx` keys, tests | Locations to be chosen during implementation; no existing file on origin/dev manages local groups | Needed only if the in-app screen is chosen |

## Test plan

Option 1 (installer and script verification, on a clean native Windows VM with a local admin):
1. Run the installer with the `networkshare` task selected; confirm `net localgroup VideoForensicsSuperUser` lists the installing user, and `net share` shows `VFReports` and `VFMedia` with the expected grants.
2. Create a local test account `vftest`; run the script to add it; confirm membership via `Get-LocalGroupMember`, then from another machine confirm write access to `\\host\VFReports`.
3. Run the script to remove it; confirm access is revoked after re-logon.
4. Re-run the script for an existing member and for a nonexistent account; confirm clear, non-destructive errors. Pester tests for the script with the `*-LocalGroupMember` cmdlets mocked.
5. Reinstall over an existing install; confirm groups and members are preserved (uninstall deliberately keeps the groups, .iss line 259).

Option 2 (TDD): write xUnit tests first for the group service (an interface faking the Windows API), the endpoint authorization (SuperAdmin only, 403 otherwise) and input validation; confirm failure, implement, confirm pass. Localization test for the page keys.

## Risks and open questions

- Product decision needed: script-only versus in-app screen. The TODO language favors in-app, but cost is large for a feature behind an opt-in checkbox.
- Security: a screen that edits local group membership is a privilege-sensitive surface; it requires the server process to hold rights to modify local groups (running as a service account, likely LocalSystem), and any bug is an evidence-exposure risk. Gate it on SuperAdmin only and audit-log every change.
- `Users:R` on the shares exposes sensitive evidence to every local user; this is outside the TODO but worth confirming with the owner.
- Account-name logging: use human-readable names; do not log SIDs or GUIDs as account identifiers per CLAUDE.md.
- The Windows-only service must not leak into MAUI, MCP or other client hosts.

## Priority recommendation

Low: product value is modest (sharing is opt-in and a documented manual workaround exists), the security exposure is unchanged by deferral (and an in-app editor would add exposure), and option 1 is small while option 2 is large. Effort: S (option 1) / L (option 2).

## Implementation dispatch

Option 1: one Haiku 5.5 subagent (`model: "haiku"`) for the script, Pester tests first, plus the .iss comment edit; option 2 only after the owner approves a separate design.
