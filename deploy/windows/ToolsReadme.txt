VideoForensics - Optional Tools
================================

This folder contains standalone command-line utilities for the VideoForensics
server. They are independent, self-contained executables - none of them need
the VideoForensics service to be running, and none of them are required for
the service to work normally (it initializes and migrates its own database on
first start regardless of whether you use these).

Each tool lives in its own subfolder so their bundled files never mix:

  Tools\DbSetup\VideoForensics.DbSetup.exe
  Tools\DbDiagnostics\VideoForensics.DbDiagnostics.exe
  Tools\DbRepair\VideoForensics.DbRepair.exe

A Start Menu shortcut ("Optional Tools" > "PowerShell in Tools Folder") opens
a PowerShell prompt already in this folder, so you can run either tool
without typing out its full path.


VideoForensics.DbSetup
-----------------------
Creates the VideoForensics database if it doesn't exist yet, and applies any
pending schema migrations if it does. Useful for provisioning a database
ahead of the very first service start (e.g. after choosing a custom data
directory during install), or for applying migrations manually after an
upgrade.

  DbSetup\VideoForensics.DbSetup.exe [--db-path <file>] [--data-root <dir>]

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
  VideoForensics.DbSetup.exe
  VideoForensics.DbSetup.exe --data-root "D:\VFData\Database"
  VideoForensics.DbSetup.exe --db-path "D:\VFData\Database\videoforensics.db"
  VideoForensics.DbSetup.exe --set-network-tier Network


VideoForensics.DbDiagnostics
------------------------------
A read-only health report on the current database: duplicate device/event
entries, redundant detection data, device health record counts, orphaned
records (foreign-key references pointing at deleted rows), and overall table
sizes. Useful for a quick sanity check if something looks off, or before
filing a support request.

  DbDiagnostics\VideoForensics.DbDiagnostics.exe

Takes no arguments - it always inspects the same default database location
the service uses. Prints its report to the console; redirect to a file if
you want to save it, e.g.:

  VideoForensics.DbDiagnostics.exe > diagnostics-report.txt


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

  DbRepair\VideoForensics.DbRepair.exe [--db-path <file>] [--data-root <dir>]
                                        [--apply] [--yes]

Safe by default: with no flags, it only PRINTS what it would delete and
makes no changes at all. Nothing is ever deleted without --apply, and even
with --apply it asks for a typed "y" confirmation before touching the
database (pass --yes too if you've already reviewed a dry run and want to
skip that prompt for a scripted run). All deletions happen in a single
transaction, so a failure partway through rolls back cleanly instead of
leaving the database half-fixed.

Recommended flow:
  VideoForensics.DbRepair.exe                    (dry run - review first)
  VideoForensics.DbRepair.exe --apply             (applies, asks to confirm)
