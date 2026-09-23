; VideoForensics Windows Installer (Inno Setup)
;
; Replaces the previous WiX-based MSI + Burn bootstrapper (removed - see git history for
; deploy/windows/VideoForensics.Bootstrapper.Wix and deploy/windows/VideoForensics.Installer.Wix).
; Switched away from WiX because WiX v5's steward (FireGiant) charges commercial users above a
; revenue threshold an "Open Source Maintenance Fee" that this proprietary app likely does not
; qualify to avoid; Inno Setup is free for any use, including closed-source commercial software,
; with no revenue-based fee. See deploy/windows/README.md for full documentation.
;
; Independently selectable components:
;   - server:  VideoForensics.WebApp, installed as a Windows Service (LocalSystem, auto-start)
;   - desktop: VideoForensics.MauiApp, a plain unpackaged desktop client (no service, no MSIX)
;   - tools:   Optional Tools - VideoForensics.DbSetup, VideoForensics.DbDiagnostics, and
;              VideoForensics.DbRepair CLI utilities (advanced/opt-in, unchecked by default)
;   - mcp:     MCP Bridge (VideoForensics.Mcp) for AI-assistant hosts like Claude Desktop
;              (advanced/opt-in, unchecked by default)
;
; Data safety: %ProgramData%\VideoForensics is NEVER referenced in [Files] or an uninstall
; delete entry, on purpose - upgrades and uninstalls must never touch the database, encryption
; keys, or media stored there. Do not add it here.
;
; Build with (from repo root):
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" deploy\windows\VideoForensics.iss ^
;     /DAppVersion=1.2.3 /DServerPublishDir=C:\path\to\server\publish /DDesktopPublishDir=C:\path\to\desktop\publish
;
; AppVersion/ServerPublishDir/DesktopPublishDir all have local-dev fallback defaults below so the
; script can also be compiled directly from a checkout for quick iteration.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef ServerPublishDir
  #define ServerPublishDir "..\..\publish\server"
#endif
#ifndef DesktopPublishDir
  #define DesktopPublishDir "..\..\publish\desktop"
#endif
#ifndef DbSetupPublishDir
  #define DbSetupPublishDir "..\..\publish\dbsetup"
#endif
#ifndef DbDiagnosticsPublishDir
  #define DbDiagnosticsPublishDir "..\..\publish\dbdiagnostics"
#endif
#ifndef DbRepairPublishDir
  #define DbRepairPublishDir "..\..\publish\dbrepair"
#endif
#ifndef McpPublishDir
  #define McpPublishDir "..\..\publish\mcp"
#endif

; Only offer the Desktop Client component/shortcuts/files when a real MauiApp publish is actually
; present at compile time. Without this guard, a local/dev compile (or a CI run where the MauiApp
; publish step failed silently) still offers "Desktop Client" as a selectable, checked-by-default
; component - the install then "succeeds" but creates a desktop shortcut pointing at an exe that
; was never copied in (Flags: skipifsourcedoesntexist on the [Files] line lets the file copy skip
; silently), producing Windows' "Missing Shortcut" dialog the first time someone double-clicks it.
#define DesktopExeExists FileExists(DesktopPublishDir + "\VideoForensics.MauiApp.exe")
; Same guard pattern for the "Optional Tools" utilities and the MCP bridge - each is its own
; separate publish that a local/dev compile may not have produced.
#define DbSetupExeExists FileExists(DbSetupPublishDir + "\VideoForensics.DbSetup.exe")
#define DbDiagnosticsExeExists FileExists(DbDiagnosticsPublishDir + "\VideoForensics.DbDiagnostics.exe")
#define DbRepairExeExists FileExists(DbRepairPublishDir + "\VideoForensics.DbRepair.exe")
#define McpExeExists FileExists(McpPublishDir + "\VideoForensics.Mcp.exe")

[Setup]
AppId={{6B1D9F0E-6A3C-4E9D-9F0B-2C7B2C2E9A11}
AppName=VideoForensics
AppVersion={#AppVersion}
AppPublisher=DV Victim Protection Team
DefaultDirName={autopf}\VideoForensics
DefaultGroupName=VideoForensics
DisableProgramGroupPage=yes
OutputBaseFilename=VideoForensicsSetup
OutputDir=bin
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
LicenseFile=..\..\LICENSE
WizardStyle=modern
UninstallDisplayName=VideoForensics

[Types]
Name: "full"; Description: "Full installation"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

; Translates only the installer wizard's own built-in chrome (Next/Back/Cancel, standard page
; titles like "Select Destination Location") - none of this script's OWN custom page text
; (Storage Configuration, Administrator Account, Network Access, Network Sharing, etc.) is
; translated, since that would require a full [CustomMessages] section per language, which is out
; of scope here. Uses Inno's built-in "compiler:" prefix so these resolve against whatever Inno
; Setup 6 install compiles this script, no hardcoded paths. Only languages whose .isl actually
; ships in this machine's Inno Setup 6\Languages folder are listed - e.g. Simplified Chinese is
; not bundled by default, so it's deliberately left out rather than breaking the compile.
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Components]
; server/desktop belong to "full" (checked by default when that type - the initial default - is
; selected) and "custom" (so they still appear, individually toggleable, if the user picks Custom).
Name: "server"; Description: "VideoForensics Server (Windows Service)"; Types: full custom
#if DesktopExeExists
Name: "desktop"; Description: "Desktop Client"; Types: full custom
#endif
; "tools" and "mcp" are advanced/opt-in - Types: custom only (no "full"), so they stay unchecked
; even under the default "Full installation" preset and only become toggleable once the user
; switches to Custom. Neither is required for the server or desktop client to work: the service
; initializes its own database on first start regardless of whether "tools" is installed (DbSetup
; just does the same thing ahead of time so the very first request isn't delayed by migrations).
#if DbSetupExeExists
Name: "tools"; Description: "Optional Tools (database setup && diagnostics utilities)"; Types: custom
#endif
#if McpExeExists
Name: "mcp"; Description: "MCP Bridge (for AI assistants such as Claude Desktop)"; Types: custom
#endif

[Tasks]
Name: "ffmpeg"; Description: "Install bundled FFmpeg (video transcoding and metadata)"; Components: server; Flags: checkedonce
#if DesktopExeExists
Name: "shortcuts"; Description: "Create Desktop && Start Menu shortcuts"; Components: desktop; Flags: checkedonce
#endif
; Deliberately NO "checkedonce" (unlike ffmpeg/shortcuts above, which mean "checked by default the
; first time only") - this must stay OFF by default on every single install/upgrade, never
; auto-checked, per explicit product decision: this data can include sensitive evidence, so sharing
; it over the network must always be an explicit opt-in click, never a default a user clicks past.
; Adds only the person running Setup to VideoForensicsSuperUser - see the TODO(user-management)
; comment on CreateNetworkShares for why nobody else is added here yet.
Name: "networkshare"; Description: "Share the Reports and Media folders on the local network (adds you to a new VideoForensicsSuperUser group - see Tools Readme for adding others)"; Components: server; Flags: unchecked

[Files]
; Server: everything from the self-contained publish output except the optional ffmpeg binaries,
; which are copied separately below only when the "ffmpeg" task is selected.
Source: "{#ServerPublishDir}\*"; DestDir: "{app}"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "ffmpeg.exe,ffprobe.exe,ffmpeg-LICENSE.txt"

Source: "{#ServerPublishDir}\ffmpeg.exe"; DestDir: "{app}"; Components: server; Tasks: ffmpeg; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#ServerPublishDir}\ffprobe.exe"; DestDir: "{app}"; Components: server; Tasks: ffmpeg; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#ServerPublishDir}\ffmpeg-LICENSE.txt"; DestDir: "{app}"; Components: server; Tasks: ffmpeg; Flags: ignoreversion skipifsourcedoesntexist

; Desktop: everything from the MauiApp self-contained publish output. Guarded by #if DesktopExeExists
; above - see that #define's comment.
#if DesktopExeExists
Source: "{#DesktopPublishDir}\*"; DestDir: "{app}\Desktop"; Components: desktop; Flags: ignoreversion recursesubdirs createallsubdirs
#endif

; Optional Tools: VideoForensics.DbSetup (create/migrate the DB ahead of first service start) and
; VideoForensics.DbDiagnostics (read-only DB health/redundancy report), grouped under the "tools"
; component. Each gets its OWN leaf subfolder, never flattened together or into {app} alongside the
; server - every one of these is an independent self-contained publish with its own copy of the
; shared-framework assemblies, and copying two self-contained outputs into the same directory risks
; one silently overwriting a same-named framework DLL from the other with a different version (this
; happened during development: an older DbSetup build shadowed the server's own
; Microsoft.AspNetCore.Http.Abstractions.dll and broke the service). Separate folders per tool make
; that class of collision structurally impossible, including between DbSetup and DbDiagnostics
; themselves now that there are two of them.
#if DbSetupExeExists
; DbSetup.exe is bundled whenever "server" is selected, NOT gated behind the separate opt-in
; "tools" component - it's used internally by the Administrator Account and Network Access pages
; below regardless of whether the user wants the "Optional Tools" (DbDiagnostics/DbRepair/PATH
; entries/shortcuts) surfaced. Gating this file on "tools" was a real bug: filling in the
; Administrator Account page silently did nothing if "Optional Tools" wasn't separately checked,
; because the exe it needed to invoke was never copied in - the browser's /setup wizard then
; became the only path to create an admin, which is a real security gap once "local network"
; access is also in play (see the Network Access race-condition warning in CurStepChanged).
Source: "{#DbSetupPublishDir}\*"; DestDir: "{app}\Tools\DbSetup"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "ToolsReadme.txt"; DestDir: "{app}\Tools"; DestName: "README.txt"; Components: tools; Flags: ignoreversion
#endif
#if DbDiagnosticsExeExists
Source: "{#DbDiagnosticsPublishDir}\*"; DestDir: "{app}\Tools\DbDiagnostics"; Components: tools; Flags: ignoreversion recursesubdirs createallsubdirs
#endif
; VideoForensics.DbRepair: applies the safe, unambiguous fixes DbDiagnostics only reports on
; (exact duplicate rows, orphaned records) - dry-run by default, requires --apply plus a typed
; confirmation (or --yes for scripted use) before touching anything. See ToolsReadme.txt.
#if DbRepairExeExists
Source: "{#DbRepairPublishDir}\*"; DestDir: "{app}\Tools\DbRepair"; Components: tools; Flags: ignoreversion recursesubdirs createallsubdirs
#endif

; MCP Bridge: stdio MCP server (VideoForensics.Mcp) that AI-assistant hosts like Claude Desktop
; launch as a subprocess and talk to over stdio, translating to HTTP calls against a VideoForensics
; server (local or remote) using a paired-device credential - it is not itself a running service and
; needs no [Run]/[UninstallRun] entries, just files at a known path the user points their MCP host's
; config at. Own top-level folder for the same self-contained-collision reason as the tools above.
#if McpExeExists
Source: "{#McpPublishDir}\*"; DestDir: "{app}\Mcp"; Components: mcp; Flags: ignoreversion recursesubdirs createallsubdirs
#endif

[Icons]
#if DesktopExeExists
Name: "{group}\VideoForensics Desktop Client"; Filename: "{app}\Desktop\VideoForensics.MauiApp.exe"; Components: desktop
Name: "{commondesktop}\VideoForensics"; Filename: "{app}\Desktop\VideoForensics.MauiApp.exe"; Components: desktop; Tasks: shortcuts
#endif

; Opens Explorer at the default Logs folder - available whenever the server component is installed
; (the server always writes logs there), not gated by "tools"/DbSetupExeExists at all. Points at the
; DEFAULT %ProgramData%\VideoForensics\Logs location only: if a custom Logs path was configured via
; the Storage Configuration pages (registry override), this shortcut won't reflect that -
; StorageLocationProvider's registry override isn't readable from a static [Icons] Filename, which
; can only be a plain constant, not Pascal code run at click time.
Name: "{group}\VideoForensics Logs Folder"; Filename: "{commonappdata}\VideoForensics\Logs"; Components: server

; Optional Tools Start Menu group - a PowerShell prompt already cd'd into the Tools folder (so
; either exe is runnable by bare name without typing the full path or subfolder), and the usage
; README for both bundled CLI tools.
#if DbSetupExeExists
Name: "{group}\Optional Tools\PowerShell in Tools Folder"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; WorkingDir: "{app}\Tools"; Components: tools
Name: "{group}\Optional Tools\Tools Readme"; Filename: "{app}\Tools\README.txt"; Components: tools
#endif

[Run]
; Service registration only runs on a genuinely fresh install - on an upgrade the service is
; already registered (stopped, not deleted, in InitializeSetup) and "sc create" would just fail.
Filename: "sc.exe"; Parameters: "create VideoForensics binPath= ""{app}\VideoForensics.WebApp.exe"" start= auto DisplayName= ""VideoForensics"""; Components: server; Check: IsFreshInstall; Flags: runhidden; StatusMsg: "Registering VideoForensics service..."
Filename: "sc.exe"; Parameters: "description VideoForensics ""VideoForensics web application and API server"""; Components: server; Check: IsFreshInstall; Flags: runhidden
Filename: "sc.exe"; Parameters: "failure VideoForensics reset= 86400 actions= restart/5000/restart/5000/restart/5000"; Components: server; Check: IsFreshInstall; Flags: runhidden

; DB init / admin-account creation / network-tier config all now run directly from
; CurStepChanged(ssPostInstall) in [Code] (via RunDbSetupTool), NOT as [Run] entries - moving them
; out of [Run] is what lets Setup actually check DbSetup.exe's exit code and react (warn) if
; something failed, instead of a [Run] entry's fire-and-forget "declarative, no way to inspect the
; result" behavior. A silent failure here specifically matters: if admin creation was requested but
; failed, the browser's /setup wizard becomes the de facto fallback, and if "local network" access
; was also chosen, ANY device on the LAN - not just the installing admin - can reach /setup first
; and claim the administrator account. See CurStepChanged for the actual calls and warnings.

; Opens the firewall for local-network access only when the user picked "Devices on my local
; network" on the Network Access page. profile=private,domain deliberately excludes "public" - do
; not auto-open the port on untrusted/public networks (e.g. coffee-shop wifi) even when local
; network access was chosen.
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""VideoForensics"" dir=in action=allow protocol=TCP localport=5162 profile=private,domain"; Components: server; Check: ShouldAddFirewallRule; Flags: runhidden; StatusMsg: "Adding firewall rule..."

Filename: "sc.exe"; Parameters: "start VideoForensics"; Components: server; Flags: runhidden; StatusMsg: "Starting VideoForensics service..."

; First-run action: open the browser to the app on a genuinely fresh install (not an upgrade), so
; the operator lands on the first-run setup flow immediately. Assumes the default Kestrel port
; (5162, per VideoForensics.WebApp/Properties/launchSettings.json) - if the port has been
; reconfigured this simply opens the wrong URL, it does not break the install.
;
; GetBrowserLauncherExe (see [Code]) launches Edge directly by its own exe path rather than going
; through "cmd.exe /c start" or explorer.exe against a URL - both of those depend on the OS already
; having a default browser association registered, which fails two different ways: (1) Setup runs
; elevated, and resolving a default browser from an elevated process can hit the wrong user
; context; (2) a fresh disposable environment (e.g. Windows Sandbox) may have NO default browser
; registered at all yet. Either produces the same "could not open link"/"no app installed" dialog.
; Passing the URL as a plain argument to msedge.exe itself sidesteps both failure modes entirely.
Filename: "{code:GetBrowserLauncherExe}"; Parameters: """http://localhost:5162"""; Components: server; Check: IsFreshInstall; Flags: postinstall skipifsilent nowait; Description: "Open VideoForensics in your browser"

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop VideoForensics"; Flags: runhidden; RunOnceId: "StopVideoForensicsSvc"
Filename: "sc.exe"; Parameters: "delete VideoForensics"; Flags: runhidden; RunOnceId: "DeleteVideoForensicsSvc"
; Harmless no-op if the rule was never added (server not installed, or "local network" never chosen).
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""VideoForensics"""; Flags: runhidden; RunOnceId: "DeleteFirewallRule"
; Harmless no-ops if the shares were never created (networkshare task never selected). The
; VideoForensicsAdmin/VideoForensicsSuperUser local groups themselves are deliberately NOT deleted
; here - group membership a domain/network admin set up shouldn't silently vanish just because the
; app was uninstalled. If they want the groups gone too, they can remove them manually via
; Computer Management > Local Users and Groups.
Filename: "net.exe"; Parameters: "share VFReports /delete"; Flags: runhidden; RunOnceId: "DeleteVFReportsShare"
Filename: "net.exe"; Parameters: "share VFMedia /delete"; Flags: runhidden; RunOnceId: "DeleteVFMediaShare"

[Code]
const
  ServiceName = 'VideoForensics';
  EventLogKey = 'SYSTEM\CurrentControlSet\Services\EventLog\Application\VideoForensics';
  { Must match StorageLocationProvider.RegistryKey exactly - this is how the server (and the
    DbSetup CLI tool, and the Windows Service) resolve a per-category custom path, read via
    StorageLocationProvider.GetDefaultRoot() before anything else touches storage. One registry
    value per category (see StorageLocationProvider.GetRegistryValueName), not a shared root -
    matching how the in-app Storage Settings feature already treats these independently. Keys is
    deliberately not offered here (see StorageLocationProvider's doc comment). }
  DataRegistryKey = 'SOFTWARE\VideoForensics';
  EnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';

var
  WasAlreadyInstalled: Boolean;
  StorageModePage: TInputOptionWizardPage;
  SimpleDirPage: TInputDirWizardPage;
  DataDirPage: TInputDirWizardPage;
  AdminSetupPage: TInputQueryWizardPage;
  NetworkBindingPage: TInputOptionWizardPage;
  ProgramDataRoot: String;
  DefaultDatabasePath, DefaultMediaPath, DefaultTempDownloadPath, DefaultLogsPath, DefaultReportsPath, DefaultBackupPath: String;
  { 0 = keep data in place (safe default), 1 = back up then delete, 2 = delete without backup. Set
    once in InitializeUninstall, acted on in CurUninstallStepChanged(usPostUninstall). Never
    defaults to a destructive value - any unexpected/interrupted dialog result falls back to 0. }
  UninstallDataChoice: Integer;

function IsServiceInstalled(const Name: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('sc.exe', 'query ' + Name, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure StopServiceIfRunning;
var
  ResultCode: Integer;
begin
  if IsServiceInstalled(ServiceName) then
  begin
    Exec('sc.exe', 'stop ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

{ True only for a genuinely fresh install (no prior service registration found). Used to skip
  re-running "sc create" on upgrades and to gate the first-run browser launch below. }
function IsFreshInstall(): Boolean;
begin
  Result := not WasAlreadyInstalled;
end;

function DbSetupToolExists(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\Tools\DbSetup\VideoForensics.DbSetup.exe'));
end;

{ True only when the Administrator Account page was actually filled in - the username field being
  non-blank is enough here, full validation (length, confirm-password match) already happened in
  NextButtonClick before the wizard let the user past that page. Reused as a [Run] Check, so it
  also needs to fail closed if the "tools" component (and therefore DbSetup.exe) isn't present. }
function ShouldCreateAdmin(): Boolean;
begin
  Result := DbSetupToolExists() and (Trim(AdminSetupPage.Values[0]) <> '');
end;

{ Builds the --set-network-tier argument for DbSetup.exe from the Network Access page's choice.
  Param is unused - required by the code-constant substitution signature used in the Run entry
  below. }
function GetNetworkTierArg(Param: String): String;
begin
  if NetworkBindingPage.SelectedValueIndex = 1 then
    Result := '--set-network-tier Network'
  else
    Result := '--set-network-tier Local';
end;

{ Runs DbSetup.exe directly (not via [Run]) so the caller can inspect whether it actually
  succeeded - Args may be '' for the bare DB-init invocation. Shows StatusText in the wizard's own
  progress label, matching what a [Run] entry's StatusMsg would have shown, so this doesn't look
  any different to the person watching the install. Returns True only if the process both launched
  AND exited 0 - a launch failure (e.g. exe missing) and a reported error are both "not success". }
function RunDbSetupTool(const Args: String; const StatusText: String): Boolean;
var
  ResultCode: Integer;
begin
  WizardForm.StatusLabel.Caption := StatusText;
  WizardForm.Update;
  Result := Exec(ExpandConstant('{app}\Tools\DbSetup\VideoForensics.DbSetup.exe'), Args, '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

{ Only true on a fresh install with the server component selected and "local network" explicitly
  chosen. Gating on IsFreshInstall matters here: on an upgrade the Network Access page is skipped
  entirely (see ShouldSkipPage), so NetworkBindingPage.SelectedValueIndex would otherwise retain
  Pascal's default of 0 - IsFreshInstall keeps that from being misread as an explicit "local only"
  choice that would somehow trigger this. }
function ShouldAddFirewallRule(): Boolean;
begin
  Result := WizardIsComponentSelected('server') and IsFreshInstall and (NetworkBindingPage.SelectedValueIndex = 1);
end;

{ Ensures a resolved data-category folder exists and is writable by the service account (LocalSystem
  - confirmed by the "sc create" line above having no "obj=" parameter, which defaults to
  LocalSystem) before the service starts. Grants an explicit Full Control ACL rather than relying on
  LocalSystem's usual implicit access, because a user-chosen custom path (e.g. a folder on a second
  drive) can carry unusual pre-existing/inherited permissions that would otherwise silently break the
  service on first start. Not a hard requirement - defense in depth - so icacls failing does not fail
  the install. /C continues past individual file errors instead of aborting the whole grant; /T
  recurses (relevant for the Database path, where DbSetup may have already created files there -
  mostly a no-op for a genuinely fresh path). }
procedure EnsureDataPathReady(const ThePath: String);
var
  ResultCode: Integer;
begin
  ForceDirectories(ThePath);
  Exec('icacls.exe', '"' + ThePath + '" /grant "NT AUTHORITY\SYSTEM:(OI)(CI)F" /T /C', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

{ Resolves the current Reports/Media paths the same way the registry-write logic in
  CurStepChanged does - Simple mode: SimpleRoot + subfolder; Advanced mode: DataDirPage.Values at
  the same indices used everywhere else in this script (Database=0/Media=1/TempDownload=2/
  Logs=3/Reports=4/Backup=5). }
function GetResolvedReportsPath(): String;
begin
  if StorageModePage.SelectedValueIndex = 0 then
    Result := SimpleDirPage.Values[0] + '\Reports'
  else
    Result := DataDirPage.Values[4];
end;

function GetResolvedMediaPath(): String;
begin
  if StorageModePage.SelectedValueIndex = 0 then
    Result := SimpleDirPage.Values[0] + '\media'
  else
    Result := DataDirPage.Values[1];
end;

{ Grants VideoForensicsAdmin/VideoForensicsSuperUser Modify and built-in Users Read on ThePath,
  matching the equal-access-for-both-groups product decision. }
procedure GrantSharingAcls(const ThePath: String);
var
  ResultCode: Integer;
begin
  Exec('icacls.exe',
    '"' + ThePath + '" /grant "VideoForensicsAdmin:(OI)(CI)M" /grant "VideoForensicsSuperUser:(OI)(CI)M" /grant "Users:(OI)(CI)R" /T /C',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

{ Feature: optional SMB sharing of the Reports/Media folders on the local network, gated on the
  "networkshare" task (unchecked by default - see [Tasks]). Creates VideoForensicsAdmin/
  VideoForensicsSuperUser local groups (idempotent - "net localgroup /add" failing because the
  group already exists on a reinstall is expected, not treated as an error), grants NTFS
  permissions, then shares both folders over SMB. Share-level ACLs (CHANGE for both groups, READ
  for Users) deliberately are not more restrictive than the NTFS layer above - NTFS remains the
  real binding constraint, matching how Windows file sharing is conventionally set up.

  TODO(user-management): this only ever seeds VideoForensicsSuperUser with the person running
  Setup (GetUserNameString()) - there's no UI here to add other Windows accounts to either group.
  For now, additional members must be added manually afterward via Computer Management > Local
  Users and Groups (or "net localgroup VideoForensicsSuperUser <name> /add"). A real fix is a
  proper user-management surface (most likely an in-app admin screen, not an installer-time one,
  since group membership is something that changes over the life of an install, not just once at
  setup) - track this as a follow-up, not a "do it later in this file" hack. }
procedure CreateNetworkShares();
var
  ResultCode: Integer;
  ReportsPath, MediaPath: String;
  CurrentUser: String;
begin
  Exec('net.exe', 'localgroup VideoForensicsAdmin /add', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('net.exe', 'localgroup VideoForensicsSuperUser /add', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  CurrentUser := GetUserNameString();
  if Trim(CurrentUser) <> '' then
    Exec('net.exe', 'localgroup VideoForensicsSuperUser "' + CurrentUser + '" /add', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  ReportsPath := GetResolvedReportsPath();
  MediaPath := GetResolvedMediaPath();

  GrantSharingAcls(ReportsPath);
  GrantSharingAcls(MediaPath);

  Exec('net.exe',
    'share VFReports="' + ReportsPath + '" /GRANT:VideoForensicsAdmin,CHANGE /GRANT:VideoForensicsSuperUser,CHANGE /GRANT:Users,READ',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('net.exe',
    'share VFMedia="' + MediaPath + '" /GRANT:VideoForensicsAdmin,CHANGE /GRANT:VideoForensicsSuperUser,CHANGE /GRANT:Users,READ',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

{ ---- System PATH management for the Optional Tools folders ------------------------------------
  Lets someone open any terminal and just type "VideoForensics.DbSetup.exe" instead of needing the
  full path or the "PowerShell in Tools Folder" shortcut. Broadcasts WM_SETTINGCHANGE afterwards so
  Explorer and freshly-opened terminals pick up the change immediately, without a reboot. }
function SendMessageTimeoutA(hWnd: Longint; Msg: Longint; wParam: Longint; lParam: string;
  fuFlags, uTimeout: Longint; var lpdwResult: Longint): Longint;
  external 'SendMessageTimeoutA@user32.dll stdcall';

{ ---- Passing the admin username/password to DbSetup.exe without a command-line argument ------
  Run-section entries inherit Setup's own process environment (there's no way to pass custom env
  vars to a Run entry directly), so SetEnvironmentVariableW sets VIDEOFORENSICS_SETUP_ADMIN_USERNAME/
  _PASSWORD on Setup's OWN process just before the Run entries execute - this affects only
  Setup's own process and anything it launches afterward, never machine-wide state (unlike
  RegWriteStringValue against the Environment key above, which IS machine-wide/persistent). }
function SetEnvironmentVariableW(lpName: String; lpValue: String): BOOL;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';

procedure BroadcastEnvironmentChange();
var
  ResultCode: Longint;
begin
  SendMessageTimeoutA($FFFF { HWND_BROADCAST }, $1A { WM_SETTINGCHANGE }, 0, 'Environment',
    2 { SMTO_ABORTIFHUNG }, 5000, ResultCode);
end;

procedure AddDirToPath(const Dir: String);
var
  CurrentPath: String;
begin
  if not RegQueryStringValue(HKLM, EnvironmentKey, 'Path', CurrentPath) then
    CurrentPath := '';

  if Pos(';' + Uppercase(Dir) + ';', ';' + Uppercase(CurrentPath) + ';') > 0 then
    exit; { already present }

  if (CurrentPath <> '') and (CurrentPath[Length(CurrentPath)] <> ';') then
    CurrentPath := CurrentPath + ';';
  CurrentPath := CurrentPath + Dir;

  RegWriteStringValue(HKLM, EnvironmentKey, 'Path', CurrentPath);
  BroadcastEnvironmentChange();
end;

procedure RemoveDirFromPath(const Dir: String);
var
  CurrentPath, Entry: String;
  Parts: TArrayOfString;
  I: Integer;
  Rebuilt: String;
begin
  if not RegQueryStringValue(HKLM, EnvironmentKey, 'Path', CurrentPath) then
    exit;

  Parts := StringSplit(CurrentPath, [';'], stAll);
  Rebuilt := '';
  for I := 0 to GetArrayLength(Parts) - 1 do
  begin
    Entry := Parts[I];
    if (Trim(Entry) = '') or (Uppercase(Entry) = Uppercase(Dir)) then
      continue; { drop this exact entry, and drop empty entries left behind by a prior removal }
    if Rebuilt <> '' then
      Rebuilt := Rebuilt + ';';
    Rebuilt := Rebuilt + Entry;
  end;

  if Rebuilt <> CurrentPath then
  begin
    RegWriteStringValue(HKLM, EnvironmentKey, 'Path', Rebuilt);
    BroadcastEnvironmentChange();
  end;
end;

{ ---- Robust "open in browser" launcher --------------------------------------------------------
  Launching a bare URL through explorer.exe (or cmd's "start") depends on the OS already having a
  default browser association registered. That's normally true on a real machine, but NOT
  guaranteed - a fresh disposable environment (e.g. Windows Sandbox) has no default browser
  configured until one has actually been run once, and hitting that gap produces the same
  "This app can't open this link"/"could not open link" dialog no matter which process launches
  the URL. Sidestep the whole default-association question by launching Edge directly by its own
  exe path with the URL as a plain argument (every browser accepts a URL as an argument regardless
  of whether it's registered as anything's default) - falling back to explorer.exe only if Edge
  genuinely isn't present (a non-standard Windows install with Edge removed). }
function FindEdgeExe(): String;
begin
  Result := ExpandConstant('{pf32}\Microsoft\Edge\Application\msedge.exe');
  if FileExists(Result) then
    exit;
  Result := ExpandConstant('{pf64}\Microsoft\Edge\Application\msedge.exe');
  if FileExists(Result) then
    exit;
  Result := '';
end;

function GetBrowserLauncherExe(Param: String): String;
var
  EdgePath: String;
begin
  EdgePath := FindEdgeExe();
  if EdgePath <> '' then
    Result := EdgePath
  else
    Result := ExpandConstant('{win}\explorer.exe');
end;

function InitializeSetup(): Boolean;
begin
  WasAlreadyInstalled := IsServiceInstalled(ServiceName);

  { Stop the service (if this is an upgrade over an existing install) before any files copy, so
    the running VideoForensics.WebApp.exe isn't locked during replace. Inno installs "on top of"
    the same AppId's directory automatically - no separate uninstall-then-reinstall dance needed. }
  StopServiceIfRunning;
  Result := True;
end;

procedure InitializeWizard();
begin
  { Resolve %ProgramData%\VideoForensics to its real literal path up front (rather than leaving
    the boxes blank - or the description text - with just a placeholder mentioning the env var)
    and pre-fill every field with its actual resolved default - matches exactly what
    StorageLocationProvider.GetDefaultRoot() computes for each category, so an unedited box
    changes nothing. Database gets its own "Database" subfolder like every other category now
    (previously it defaulted to the bare root, inconsistent with media/Logs/Reports/backup). }
  ProgramDataRoot := ExpandConstant('{commonappdata}') + '\VideoForensics';
  DefaultDatabasePath := ProgramDataRoot + '\Database';
  DefaultMediaPath := ProgramDataRoot + '\media';
  DefaultTempDownloadPath := ProgramDataRoot + '\temp';
  DefaultLogsPath := ProgramDataRoot + '\Logs';
  DefaultReportsPath := ProgramDataRoot + '\Reports';
  DefaultBackupPath := ProgramDataRoot + '\backup';

  { Storage mode choice - most installs only ever need one folder; the six-field Advanced page
    below is for the minority who want to split data across drives. Only meaningful on a fresh
    install: changing it on an upgrade would silently orphan existing data at the old location
    rather than moving it, so ShouldSkipPage hides all three of these pages on upgrades. }
  StorageModePage := CreateInputOptionPage(wpSelectTasks,
    'Storage Configuration', 'How should VideoForensics organize its data?',
    'Most installs only need one folder. Choose Advanced only if you need to put different kinds ' +
    'of data (database, media, logs, etc.) in different locations.',
    True, False);
  StorageModePage.Add('Simple - use a single folder for everything (recommended)');
  StorageModePage.Add('Advanced - choose a separate location for each data type');
  StorageModePage.SelectedValueIndex := 0;

  SimpleDirPage := CreateInputDirPage(StorageModePage.ID,
    'Data Location', 'Where should VideoForensics store its data?',
    'Database, media, logs, reports, and backups will each be organized into their own subfolder ' +
    'under this location.',
    False, '');
  SimpleDirPage.Add('Data folder:');
  SimpleDirPage.Values[0] := ProgramDataRoot;

  { Advanced: one box per storage category (matching how the in-app Storage Settings page already
    treats these independently). }
  DataDirPage := CreateInputDirPage(SimpleDirPage.ID,
    'Data Directory', 'Where should VideoForensics store each type of data?',
    'Each field defaults to a subfolder under ' + ProgramDataRoot + '. Change only the ones you ' +
    'specifically need on a different drive or path - leave the rest as they are.',
    False, '');
  DataDirPage.Add('Database:');
  DataDirPage.Add('Media:');
  DataDirPage.Add('Temporary downloads:');
  DataDirPage.Add('Logs:');
  DataDirPage.Add('Reports:');
  DataDirPage.Add('Backups:');

  DataDirPage.Values[0] := DefaultDatabasePath;
  DataDirPage.Values[1] := DefaultMediaPath;
  DataDirPage.Values[2] := DefaultTempDownloadPath;
  DataDirPage.Values[3] := DefaultLogsPath;
  DataDirPage.Values[4] := DefaultReportsPath;
  DataDirPage.Values[5] := DefaultBackupPath;

  { Optional initial SuperAdmin account. Leaving both fields blank (the default - nothing
    pre-filled) skips this entirely; the app's own /setup page handles it in the browser instead.
    Validated in NextButtonClick below, never here. }
  AdminSetupPage := CreateInputQueryPage(DataDirPage.ID,
    'Administrator Account', 'Create the initial administrator account (optional)',
    'Leave both fields blank to skip this - the app''s own first-run setup page will appear in ' +
    'your browser instead.');
  AdminSetupPage.Add('Username:', False);
  AdminSetupPage.Add('Password:', True);
  AdminSetupPage.Add('Confirm Password:', True);

  { Network binding choice - who besides this machine can reach the server. Internet access (via
    Cloudflare Tunnel) is deliberately NOT offered here - that's configured later from Settings >
    Remote Access inside the app once installed, since it requires signing in to Cloudflare. }
  NetworkBindingPage := CreateInputOptionPage(AdminSetupPage.ID,
    'Network Access', 'Who can access VideoForensics?',
    'Local machine only is the most secure option. Local network allows other devices on your ' +
    'network to connect. Internet access via Cloudflare Tunnel can be configured later from ' +
    'Settings > Remote Access inside the app once installed - it requires signing in to ' +
    'Cloudflare and is not offered here.',
    True, False);
  NetworkBindingPage.Add('This computer only (recommended)');
  NetworkBindingPage.Add('Devices on my local network');
  NetworkBindingPage.SelectedValueIndex := 0;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
var
  NotEligible: Boolean;
begin
  Result := False;
  NotEligible := WasAlreadyInstalled or not WizardIsComponentSelected('server');

  if (PageID = StorageModePage.ID) and NotEligible then
  begin
    Result := True;
    exit;
  end;

  if (PageID = SimpleDirPage.ID) and (NotEligible or (StorageModePage.SelectedValueIndex <> 0)) then
  begin
    Result := True;
    exit;
  end;

  if (PageID = DataDirPage.ID) and (NotEligible or (StorageModePage.SelectedValueIndex = 0)) then
  begin
    Result := True;
    exit;
  end;

  if (PageID = AdminSetupPage.ID) and NotEligible then
  begin
    Result := True;
    exit;
  end;

  if (PageID = NetworkBindingPage.ID) and NotEligible then
  begin
    Result := True;
  end;
end;

{ Validates the Administrator Account page before letting the wizard advance past it. Blank/blank
  is the explicit skip case (allowed through untouched) - the app's own /setup page is the fallback.
  Anything else must be a fully-formed account: non-blank username, a password meeting DbSetup.exe's
  own >= 12 character minimum (checked here too so the user gets immediate feedback instead of a
  silent DbSetup.exe exit code 1 later), and a matching confirmation. }
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Username, Password, ConfirmPassword: String;
begin
  Result := True;

  if CurPageID = AdminSetupPage.ID then
  begin
    Username := AdminSetupPage.Values[0];
    Password := AdminSetupPage.Values[1];
    ConfirmPassword := AdminSetupPage.Values[2];

    if (Trim(Username) = '') and (Password = '') and (ConfirmPassword = '') then
    begin
      Result := True;
      exit;
    end;

    if Trim(Username) = '' then
    begin
      MsgBox('Enter a username for the administrator account, or leave all three fields blank to skip account creation.', mbError, MB_OK);
      Result := False;
      exit;
    end;

    if Length(Password) < 12 then
    begin
      MsgBox('The administrator password must be at least 12 characters long.', mbError, MB_OK);
      Result := False;
      exit;
    end;

    if Password <> ConfirmPassword then
    begin
      MsgBox('The password and confirmation password do not match.', mbError, MB_OK);
      Result := False;
      exit;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  SimpleRoot: String;
  AdminCreationRequested: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    { Event Log source registration - equivalent of WiX's never-finished util:EventSource TODO. }
    if WizardIsComponentSelected('server') then
    begin
      RegWriteStringValue(HKLM, EventLogKey, 'EventMessageFile', ExpandConstant('{app}\VideoForensics.WebApp.exe'));
      RegWriteDWordValue(HKLM, EventLogKey, 'TypesSupported', 7);
    end;

    { Upgrade case: pre-create/migrate the database. On a fresh install this instead happens further
      below, AFTER the custom data path (if any) is resolved and made ready - an upgrade already has
      an established path from the original install, so there's nothing to wait on here. See
      RunDbSetupTool's doc comment for why this runs directly here instead of as a [Run] entry. }
    if WizardIsComponentSelected('server') and (not IsFreshInstall) and DbSetupToolExists() then
      RunDbSetupTool('', 'Initializing database...');

    { Custom data paths - written before the service starts (see the [Run] ordering above) so
      StorageLocationProvider.GetDefaultRoot() sees them on the very first startup. Only a field
      the user actually edited (differs from its pre-filled default) gets a registry entry - an
      untouched box should change nothing, not "helpfully" pin today's default forever. }
    if WizardIsComponentSelected('server') and IsFreshInstall then
    begin
      if StorageModePage.SelectedValueIndex = 0 then
      begin
        { Simple mode: one root folder, subfolder-per-category laid out exactly the way
          StorageLocationProvider.GetDefaultRoot() lays out the %ProgramData% default. }
        SimpleRoot := SimpleDirPage.Values[0];
        if (Trim(SimpleRoot) <> '') and (SimpleRoot <> ProgramDataRoot) then
        begin
          RegWriteStringValue(HKLM, DataRegistryKey, 'DatabasePath', SimpleRoot + '\Database');
          RegWriteStringValue(HKLM, DataRegistryKey, 'MediaPath', SimpleRoot + '\media');
          RegWriteStringValue(HKLM, DataRegistryKey, 'TempDownloadPath', SimpleRoot + '\temp');
          RegWriteStringValue(HKLM, DataRegistryKey, 'LogsPath', SimpleRoot + '\Logs');
          RegWriteStringValue(HKLM, DataRegistryKey, 'ReportsPath', SimpleRoot + '\Reports');
          RegWriteStringValue(HKLM, DataRegistryKey, 'BackupPath', SimpleRoot + '\backup');
        end;

        { Ensure every resolved path exists and is writable by the service account before the
          service starts - unconditional for all 6, not just ones the user edited: even the
          untouched default might not exist yet on a truly fresh machine. }
        EnsureDataPathReady(SimpleRoot + '\Database');
        EnsureDataPathReady(SimpleRoot + '\media');
        EnsureDataPathReady(SimpleRoot + '\temp');
        EnsureDataPathReady(SimpleRoot + '\Logs');
        EnsureDataPathReady(SimpleRoot + '\Reports');
        EnsureDataPathReady(SimpleRoot + '\backup');
      end
      else
      begin
        if (Trim(DataDirPage.Values[0]) <> '') and (DataDirPage.Values[0] <> DefaultDatabasePath) then
          RegWriteStringValue(HKLM, DataRegistryKey, 'DatabasePath', DataDirPage.Values[0]);
        if (Trim(DataDirPage.Values[1]) <> '') and (DataDirPage.Values[1] <> DefaultMediaPath) then
          RegWriteStringValue(HKLM, DataRegistryKey, 'MediaPath', DataDirPage.Values[1]);
        if (Trim(DataDirPage.Values[2]) <> '') and (DataDirPage.Values[2] <> DefaultTempDownloadPath) then
          RegWriteStringValue(HKLM, DataRegistryKey, 'TempDownloadPath', DataDirPage.Values[2]);
        if (Trim(DataDirPage.Values[3]) <> '') and (DataDirPage.Values[3] <> DefaultLogsPath) then
          RegWriteStringValue(HKLM, DataRegistryKey, 'LogsPath', DataDirPage.Values[3]);
        if (Trim(DataDirPage.Values[4]) <> '') and (DataDirPage.Values[4] <> DefaultReportsPath) then
          RegWriteStringValue(HKLM, DataRegistryKey, 'ReportsPath', DataDirPage.Values[4]);
        if (Trim(DataDirPage.Values[5]) <> '') and (DataDirPage.Values[5] <> DefaultBackupPath) then
          RegWriteStringValue(HKLM, DataRegistryKey, 'BackupPath', DataDirPage.Values[5]);

        { Same "unconditional for all 6" reasoning as the Simple-mode branch above. }
        EnsureDataPathReady(DataDirPage.Values[0]);
        EnsureDataPathReady(DataDirPage.Values[1]);
        EnsureDataPathReady(DataDirPage.Values[2]);
        EnsureDataPathReady(DataDirPage.Values[3]);
        EnsureDataPathReady(DataDirPage.Values[4]);
        EnsureDataPathReady(DataDirPage.Values[5]);
      end;

      { Fresh-install case: the custom path (if any) is now resolved and ready, so it's safe to
        pre-create/migrate the database - must happen before admin-account creation below, which
        needs the Operators table to already exist. }
      if DbSetupToolExists() then
        RunDbSetupTool('', 'Initializing database...');

      { Carries the optional admin username/password to DbSetup.exe via environment variables
        rather than CLI args (see SetEnvironmentVariableW's doc comment). Deliberately left set for
        the remainder of Setup's own short-lived process lifetime rather than cleared immediately
        after - there's no reliable "after this specific call" hook to clear them at, and Setup's
        own process exits shortly after ssDone anyway. This is NOT a lingering system-wide env var:
        SetEnvironmentVariableW only affects Setup's OWN process and anything it launches
        afterward, never machine-wide state. }
      AdminCreationRequested := ShouldCreateAdmin();
      if AdminCreationRequested then
      begin
        SetEnvironmentVariableW('VIDEOFORENSICS_SETUP_ADMIN_USERNAME', AdminSetupPage.Values[0]);
        SetEnvironmentVariableW('VIDEOFORENSICS_SETUP_ADMIN_PASSWORD', AdminSetupPage.Values[1]);
        if not RunDbSetupTool('', 'Creating administrator account...') then
        begin
          MsgBox('The administrator account could not be created automatically (DbSetup.exe ' +
            'reported an error). VideoForensics will fall back to its browser-based first-run ' +
            'setup page instead - open it yourself as soon as the install finishes, since ' +
            'ANYONE who reaches that page first (including another device, if you chose local ' +
            'network access) will be able to create the administrator account.', mbError, MB_OK);
        end;
      end;

      { Records the Network Access page's choice - runs unconditionally whenever the tool is
        present, even the default "Local" choice, matching the app's own default rather than
        leaving the setting unset. }
      if DbSetupToolExists() then
        RunDbSetupTool(GetNetworkTierArg(''), 'Configuring network access...');

      { The specific dangerous combination this whole block exists to catch: local network access
        was chosen, but no administrator account was set up here to close the race before it
        starts. Whoever reaches /setup first wins - make that risk explicit rather than silent. }
      if (NetworkBindingPage.SelectedValueIndex = 1) and (not AdminCreationRequested) then
      begin
        MsgBox('You chose to allow devices on your local network to access VideoForensics, but ' +
          'did not set up an administrator account on the previous page. The FIRST device to ' +
          'browse to this server - not necessarily you - will be able to create the ' +
          'administrator account. Open VideoForensics yourself immediately after this install ' +
          'finishes to claim it first.', mbConfirmation, MB_OK);
      end;

      { Network Sharing: only when the "networkshare" task was explicitly selected (unchecked by
        default - see [Tasks] above). Creates two local groups (idempotent - "net localgroup /add"
        returning non-zero because the group already exists on a reinstall is fine, not a failure),
        adds each entered username to VideoForensicsSuperUser, grants NTFS permissions on the
        Reports/Media paths, then shares them over SMB. }
      if WizardIsComponentSelected('server') and IsFreshInstall and WizardIsTaskSelected('networkshare') then
      begin
        CreateNetworkShares();
      end;
    end;

    { Add each installed Optional Tool's own subfolder to the system PATH, so they're runnable by
      bare filename from any terminal. Safe to call on every install/upgrade where "tools" is
      selected - AddDirToPath is a no-op if the entry is already present. }
    if WizardIsComponentSelected('tools') then
    begin
      if DirExists(ExpandConstant('{app}\Tools\DbSetup')) then
        AddDirToPath(ExpandConstant('{app}\Tools\DbSetup'));
      if DirExists(ExpandConstant('{app}\Tools\DbDiagnostics')) then
        AddDirToPath(ExpandConstant('{app}\Tools\DbDiagnostics'));
      if DirExists(ExpandConstant('{app}\Tools\DbRepair')) then
        AddDirToPath(ExpandConstant('{app}\Tools\DbRepair'));
    end;
  end;
end;

{ Asks what to do with %ProgramData%\VideoForensics before uninstalling. This data can be very
  large (database, media, logs, keys) and was previously deleted by accident during development
  by a script that assumed a batch of commands had uniformly failed - it hadn't. This prompt exists
  so that decision is always explicit and never a silent default:
    - "Cancel uninstall" genuinely aborts the whole uninstall (Result := False) - Cancel here means
      what a user expects it to mean, it is never repurposed as a destructive choice.
    - "Delete the data" leads to a SECOND, separate confirmation (back up first, or delete with no
      backup) - a single click can never permanently destroy this data.
    - Any unexpected dialog outcome (window closed, etc.) falls back to UninstallDataChoice = 0
      (keep), never to a delete path. }
function InitializeUninstall(): Boolean;
var
  DataPath: String;
  Response: Integer;
  Labels: TArrayOfString;
begin
  Result := True;
  UninstallDataChoice := 0;
  DataPath := ExpandConstant('{commonappdata}') + '\VideoForensics';

  if not DirExists(DataPath) then
  begin
    exit;
  end;

  SetArrayLength(Labels, 3);
  Labels[0] := '&Keep the data (recommended)' + #13#10 + 'Leave everything in place, untouched.';
  Labels[1] := '&Delete the data' + #13#10 + 'Choose whether to back it up first on the next screen.';
  Labels[2] := '&Cancel uninstall' + #13#10 + 'Do not uninstall VideoForensics right now.';

  Response := TaskDialogMsgBox(
    'What should happen to your VideoForensics data?',
    'Your database, media, logs, and encryption keys are stored in:' + #13#10 + DataPath + #13#10#13#10 +
    'This can be a large amount of data - choose carefully.',
    mbConfirmation,
    MB_YESNOCANCEL,
    Labels,
    IDYES);

  case Response of
    IDYES:
      UninstallDataChoice := 0;
    IDNO:
      begin
        SetArrayLength(Labels, 3);
        Labels[0] := '&Back up, then delete' + #13#10 + 'Safer - keeps a copy first.';
        Labels[1] := '&Delete immediately, no backup' + #13#10 + 'Cannot be undone.';
        Labels[2] := '&Keep the data instead' + #13#10 + 'Change my mind - leave everything in place.';

        Response := TaskDialogMsgBox(
          'This will permanently delete your VideoForensics data.',
          'Back up ' + DataPath + ' to a timestamped folder first, or delete it immediately ' +
          'without a backup?' + #13#10#13#10 +
          'If your data is large, backing up will take longer but gives you a way back.',
          mbConfirmation,
          MB_YESNOCANCEL,
          Labels,
          IDYES);
        case Response of
          IDYES: UninstallDataChoice := 1;
          IDNO: UninstallDataChoice := 2;
        else
          UninstallDataChoice := 0;
        end;
      end;
  else
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataPath, BackupPath: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKLM, EventLogKey);

    { Unconditional and harmless if never added - RemoveDirFromPath is a no-op when the entry
      isn't present. }
    RemoveDirFromPath(ExpandConstant('{app}\Tools\DbSetup'));
    RemoveDirFromPath(ExpandConstant('{app}\Tools\DbDiagnostics'));
    RemoveDirFromPath(ExpandConstant('{app}\Tools\DbRepair'));

    DataPath := ExpandConstant('{commonappdata}') + '\VideoForensics';

    if (UninstallDataChoice = 1) and DirExists(DataPath) then
    begin
      BackupPath := DataPath + '.backup-' + GetDateTimeString('yyyymmdd-hhnnss', #0, #0);
      if Exec('robocopy.exe',
              '"' + DataPath + '" "' + BackupPath + '" /E /COPYALL /R:2 /W:5 /MT:16',
              '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      begin
        { robocopy exit codes 0-7 mean success/informational; 8+ means a real failure. Only
          delete the original once the backup is confirmed to have actually succeeded. }
        if ResultCode < 8 then
        begin
          DelTree(DataPath, True, True, True);
        end
        else
        begin
          MsgBox('The backup to ' + BackupPath + ' may have failed (robocopy exit code ' +
                 IntToStr(ResultCode) + '). Your original data was left in place at ' + DataPath +
                 ' to be safe - nothing was deleted.', mbError, MB_OK);
        end;
      end;
    end
    else if (UninstallDataChoice = 2) and DirExists(DataPath) then
    begin
      DelTree(DataPath, True, True, True);
    end;
  end;
end;
