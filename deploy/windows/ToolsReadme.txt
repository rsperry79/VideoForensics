VideoForensics - Optional Tools
================================

This folder contains standalone command-line utilities for the VideoForensics
server. They are independent, self-contained executables - none of them need
the VideoForensics service to be running, and none of them are required for
the service to work normally (it initializes and migrates its own database on
first start regardless of whether you use these).

Each tool lives in its own subfolder so their bundled files never mix:

  Tools\DbSetup\VideoForensics.Utils.DbSetup.exe
  Tools\DbDiagnostics\VideoForensics.Utils.Diagnostics.exe
  Tools\DbRepair\VideoForensics.Utils.DbRepair.exe
  Tools\LogViewer\VideoForensics.Utils.LogViewer.exe

A Start Menu shortcut ("Optional Tools" > "PowerShell in Tools Folder") opens
a PowerShell prompt already in this folder, so you can run any of these tools
without typing out its full path. The "Optional Tools" > "Log Viewer" shortcut
opens the log viewer directly.


VideoForensics.DbSetup
-----------------------
Creates the VideoForensics database if it doesn't exist yet, and applies any
pending schema migrations if it does. Useful for provisioning a database
ahead of the very first service start (e.g. after choosing a custom data
directory during install), or for applying migrations manually after an
upgrade.

  DbSetup\VideoForensics.Utils.DbSetup.exe [--db-path <file>] [--data-root <dir>]

  --db-path <file>          Exact path to the SQLite database file. Takes
                             priority over --data-root.
  --data-root <dir>         Directory to place videoforensics.db in.
                             Ignored if --db-path is set.
  --set-network-tier <tier> Sets who can reach the server: "Local" (this
                             machine only) or "Network" (other devices on
                             your LAN). Does not affect the database
                             create/migrate step - runs independently.
                             (Internet/Cloudflare access isn't settable
                             here - configure it in-app under Settings >
                             Remote Access after installing.)
  --help, -h                Show usage and exit.

With no arguments, it uses the same default path the service itself would
use (the effective Database location from Settings > Storage, or the
%ProgramData%\VideoForensics\Database default if nothing has been
customized).

Creating the initial administrator account is not a command-line flag -
set the VIDEOFORENSICS_SETUP_ADMIN_USERNAME and
VIDEOFORENSICS_SETUP_ADMIN_PASSWORD environment variables before running
this tool (password must be 12+ characters) and it will create that
SuperAdmin account, unless one already exists (safe to re-run - it skips
silently rather than creating a duplicate). Environment variables, not a
CLI argument, so the password never appears in a process listing.

Examples:
  VideoForensics.Utils.DbSetup.exe
  VideoForensics.Utils.DbSetup.exe --data-root "D:\VFData\Database"
  VideoForensics.Utils.DbSetup.exe --db-path "D:\VFData\Database\videoforensics.db"
  VideoForensics.Utils.DbSetup.exe --set-network-tier Network


VideoForensics.DbDiagnostics
------------------------------
A read-only health report on the current database: duplicate device/event
entries, redundant detection data, device health record counts, orphaned
records (foreign-key references pointing at deleted rows), and overall table
sizes. Useful for a quick sanity check if something looks off, or before
filing a support request.

  DbDiagnostics\VideoForensics.Utils.Diagnostics.exe

Takes no arguments - it always inspects the same default database location
the service uses. Prints its report to the console; redirect to a file if
you want to save it, e.g.:

  VideoForensics.Utils.Diagnostics.exe > diagnostics-report.txt


VideoForensics.DbRepair
-------------------------
Fixes the issues DbDiagnostics can only report on - but ONLY the ones with
an unambiguous, safe fix: exact duplicate rows (same device or event
re-recorded under the same provider ID - the earliest copy is kept) and
orphaned records (a row whose parent no longer exists, so it's already
unreachable/meaningless data). Nothing else is touched automatically -
redundant detection data and device feature/capability overlap are still
DbDiagnostics-report-only, since those need a human judgment call about
which data is authoritative.

  DbRepair\VideoForensics.Utils.DbRepair.exe [--db-path <file>] [--data-root <dir>]
                                        [--apply] [--yes]

Safe by default: with no flags, it only PRINTS what it would delete and
makes no changes at all. Nothing is ever deleted without --apply, and even
with --apply it asks for a typed "y" confirmation before touching the
database (pass --yes too if you've already reviewed a dry run and want to
skip that prompt for a scripted run). All deletions happen in a single
transaction, so a failure partway through rolls back cleanly instead of
leaving the database half-fixed.

Recommended flow:
  VideoForensics.Utils.DbRepair.exe                    (dry run - review first)
  VideoForensics.Utils.DbRepair.exe --apply             (applies, asks to confirm)


SuperAdmin Password Recovery (--reset-superadmin-password)
-----------------------------------------------------------
Recovers SuperAdmin access when the passkey is lost. Prompts interactively
for a new password (minimum 12 characters) with confirmation, then resets it,
unlocks the account, re-activates it, and invalidates all existing sessions.

  DbRepair\VideoForensics.Utils.DbRepair.exe --reset-superadmin-password
                                       [--username <name>]
                                       [--db-path <file>]
                                       [--data-root <dir>]
                                       [--yes]

  --reset-superadmin-password  Enable recovery mode.
  --username <name>            Target operator username. If omitted, uses the
                               primary SuperAdmin (or the single SuperAdmin
                               if there's only one).
  --db-path <file>             Exact path to the SQLite database file.
  --data-root <dir>            Directory containing videoforensics.db.
  --yes                        Skip confirmation prompt. Use only if you've
                               already reviewed and accepted the action.

IMPORTANT NOTES:
  - Must run on the server machine with administrator privileges (database
    is under ProgramData).
  - Stop the VideoForensics Windows service before running this tool to avoid
    SQLite WAL (Write-Ahead Logging) conflicts if concurrent writes occur.
  - The password prompt is ALWAYS interactive - it cannot be passed via
    command-line argument or environment variable for security reasons.
  - Password is never logged or echoed to the terminal.
  - Existing sessions for the recovered account are immediately invalidated.

Example:
  VideoForensics.Utils.DbRepair.exe --reset-superadmin-password
  VideoForensics.Utils.DbRepair.exe --reset-superadmin-password --username admin


VideoForensics.Utils.LogViewer
-------------------------------
A console viewer for the WebApp's logger output. The WebApp publishes each
log entry to the named pipe "VideoForensics.Logger" while a client is
connected; this tool connects to that pipe and prints each entry as it is
written. It reconnects on its own if the WebApp restarts, and Ctrl+C exits.

  LogViewer\VideoForensics.Utils.LogViewer.exe [--pipe <name>] [--raw]

  --pipe <name>  Pipe to connect to (default: VideoForensics.Logger).
  --raw          Print each line as received instead of formatting the JSON.
  --help, -h     Show usage and exit.

The pipe is closed by default. Open it in the VideoForensics app under
Settings > Infrastructure > Diagnostics (SuperAdmin, or Admin once granted
access on the Permissions page). The change applies immediately, and the saved
value is read again when the service starts. Until it is open, this viewer
waits without showing anything.
