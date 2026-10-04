# Phase 4: Logger Viewer & Named Pipe Logging Infrastructure

## Scope
Implement named-pipe based logging, WPF desktop LoggerViewer app, password recovery for passkey loss, and integrations.

## Changes by Item

### Item 1: Password Recovery (Localhost-Only)

**Requirement:** Enable SuperAdmin to reset password via localhost-only endpoint in case passkey is lost. Password login becomes an alternative auth method with required password change on first use.

**Implementation:**

1. **WebApp Auth Endpoint:**
   - New: `POST /api/v1/auth/recover-password` (localhost-only check via `HttpContext.Connection.IsLocal()`)
   - Returns: `{ "temporaryPassword": "generated-string", "expiresAtUtc": "timestamp" }`
   - Logic:
     - Check `HttpContext.Connection.IsLocal()` — reject if not localhost (403 Forbidden)
     - Check if any operator exists — reject if none (404 Not Found, prevents setup endpoint misuse)
     - Generate 16-char random alphanumeric password
     - Hash and store in Operator table (same as normal password hash)
     - Set `MustChangePassword = true` on SuperAdmin operator
     - Return temp password (this is the only time it's shown; never logged or stored in plaintext)
   - Idempotent: calling multiple times regenerates; each call overwrites previous temp password

2. **Login Flow Enhancement:**
   - Modify: `POST /api/v1/auth/login` to accept password as fallback
   - Current: passkey-only
   - New: try passkey first, then fallback to username+password if passkey fails
   - On successful password login with `MustChangePassword = true`:
     - Return auth token but signal `"passwordChangeRequired": true` in response
     - Client redirects to `/profile/change-password` before accessing other routes

3. **Password Change Enforcement:**
   - Modify: `AuthGate.razor` / auth middleware
     - If user is authenticated but `MustChangePassword = true`, redirect to `/profile/change-password`
     - Block all other routes until password is changed
   - Modify: `POST /api/v1/auth/change-password` (existing endpoint)
     - On success, set `MustChangePassword = false`
     - Log password change with audit trail (Info level, "SuperAdmin password reset via recovery" message)

4. **Logging & Audit:**
   - Log `/api/v1/auth/recover-password` calls (Info: "Localhost password recovery requested")
   - Log password login attempts (Info: "Password login successful", Error: "Password login failed")
   - Log password change (Info: "Password changed; MustChangePassword cleared")

**Security Notes:**
- Localhost-only check via `HttpContext.Connection.IsLocal()` — blocks remote access
- Temp password is single-use (overwritten on next call to recover endpoint)
- `MustChangePassword = true` forces immediate password change before any other operations
- Passkey remains the primary auth method; password is recovery-only fallback
- No config/flag needed — endpoint is always available to localhost

### Item 2: Logger Viewer GUI App (Named Pipe Consumer)

**Architecture:**
- Server opens named pipe: `\\.\pipe\VideoForensics.Logger`
- Writes entries as newline-delimited JSON: `{ "timestamp": "...", "level": "...", "message": "..." }`
- LoggerViewer connects to pipe and displays live incoming messages in real-time
- Circular buffer: keep last 500 entries in memory

**Server-side implementation:**

1. **Core Logging:**
   - New/Modify: `src/core/core/core.logging/NamedPipeLoggerProvider.cs`
     - Implements ILoggerProvider
     - Opens named pipe on server startup (Windows platform only)
     - Writes all log entries asynchronously (JSON format)
     - Maintains circular buffer (500 entries max)
     - Graceful handling of client disconnect

2. **Dependency Injection:**
   - Modify: `src/client/host/VideoForensics.Hosting/DependencyInjection/` logging setup
     - Add NamedPipeLoggerProvider to logging pipeline (conditional: Windows only)
     - Configuration setting: `Logging:NamedPipeEnabled` (default: true on Windows)

**Client-side (LoggerViewer):**

1. **Desktop App:**
   - New project: `src/utils/logger/VideoForensics.Utils.LoggerViewer.csproj` (WPF)
     - Connects to named pipe on startup
     - Displays log entries in scrollable list (auto-scroll to bottom)
     - Shows: Timestamp, Level (colored), Message
     - Filter by level dropdown (All, Debug, Info, Warning, Error, Critical)
     - Clear button (clears display only, not server logs)
     - Save button (export visible logs to CSV/TXT)
     - Connection status display (Connected/Disconnected/Reconnecting)
     - Auto-reconnect on disconnect (exponential backoff, max 30s)

2. **MAUI Integration:**
   - Modify: `src/client/maui/VideoForensics.MauiApp/MauiProgram.cs`
     - Add menu item: "Dev Tools → Logger Viewer" (Windows-only, SuperAdmin on localhost)
     - On click: launch LoggerViewer.exe or open embedded web viewer

**Security:**
- Named pipe permissions: Restrict to SYSTEM and Administrators group (Windows ACLs)
- LoggerViewer must run with sufficient privileges (Admin or same user as server)

## Testing Checklist

### Password Recovery (Item 1)
- [ ] `/api/v1/auth/recover-password` localhost-only enforcement
  - [ ] Local call returns temp password (200 OK)
  - [ ] Remote call returns 403 Forbidden (simulate with `X-Forwarded-For` header if needed)
- [ ] Temp password is usable for login
  - [ ] POST `/api/v1/auth/login` with username + temp password succeeds
  - [ ] Response includes `"passwordChangeRequired": true`
- [ ] `MustChangePassword` enforcement
  - [ ] Accessing `/profile/change-password` while `MustChangePassword = true` is allowed
  - [ ] Accessing other protected routes redirects to `/profile/change-password`
  - [ ] Changing password sets `MustChangePassword = false`
  - [ ] After password change, all routes are accessible
- [ ] Idempotency
  - [ ] Calling recover-password twice overwrites first temp password (second one works, first doesn't)
- [ ] Logging
  - [ ] Recovery request logged (Info level)
  - [ ] Password login attempt logged (Info/Error)
  - [ ] Password change logged (Info level)

### Server-side
- [ ] NamedPipeLoggerProvider compiles
  - [ ] `dotnet build src/core/core/core.logging/` succeeds

- [ ] Named pipe creation and logging
  - [ ] Start server → pipe `\\.\pipe\VideoForensics.Logger` created
  - [ ] Log entries written to pipe in real-time
  - [ ] Graceful handling when no reader connected
  - [ ] Graceful handling when reader disconnects

- [ ] Configuration
  - [ ] `Logging:NamedPipeEnabled=false` disables pipe (no-op)
  - [ ] On non-Windows platform: pipe disabled (graceful no-op)

### LoggerViewer Desktop App
- [ ] WPF project builds without errors
- [ ] Executable launches and connects to pipe
- [ ] Live log entries display in real-time
- [ ] Display auto-scrolls to bottom as entries arrive
- [ ] Filter dropdown works (shows only selected level)
- [ ] Clear button clears display
- [ ] Save button exports visible logs to file (CSV or TXT format)
  - [ ] File includes: Timestamp, Level, Message columns
- [ ] Connection status shown in title bar or footer
- [ ] Auto-reconnect works on server restart
  - [ ] Shows "Disconnected" during gap
  - [ ] Shows "Reconnecting..." with exponential backoff
  - [ ] Shows "Connected" when successful
- [ ] Recent history displayed on first connect (last 500 entries)
- [ ] No errors on pipe disconnect/reconnect cycle

### MAUI Integration
- [ ] Windows platform: "Dev Tools → Logger Viewer" menu visible (SuperAdmin role + localhost)
- [ ] Non-Windows platform: menu item not shown
- [ ] Menu item click launches LoggerViewer.exe
- [ ] Error handling if LoggerViewer not installed

### Error Handling
- [ ] Failed account sync logs as Error level (visible in LoggerViewer)
- [ ] Error messages include account name, error details, timestamp

## Dependencies
- Phase 3 must be complete (LoggerViewer goes in src/utils/logger/)

## Next Phase
None (complete implementation of all 4 phases)
