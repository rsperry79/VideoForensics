; VideoForensics Windows Installer (Inno Setup)
;
; Replaces the previous WiX-based MSI + Burn bootstrapper (removed - see git history for
; deploy/windows/VideoForensics.Bootstrapper.Wix and deploy/windows/VideoForensics.Installer.Wix).
; Switched away from WiX because WiX v5's steward (FireGiant) charges commercial users above a
; revenue threshold an "Open Source Maintenance Fee" that this proprietary app likely does not
; qualify to avoid; Inno Setup is free for any use, including closed-source commercial software,
; with no revenue-based fee. See deploy/windows/README.md for full documentation.
;
; Two independently selectable components:
;   - server:  VideoForensics.WebApp, installed as a Windows Service (LocalSystem, auto-start)
;   - desktop: VideoForensics.MauiApp, a plain unpackaged desktop client (no service, no MSIX)
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

[Components]
; Both components belong to "full" (checked by default when that type - the initial default - is
; selected) and "custom" (so they still appear, individually toggleable, if the user picks Custom).
Name: "server"; Description: "VideoForensics Server (Windows Service)"; Types: full custom
Name: "desktop"; Description: "Desktop Client"; Types: full custom

[Tasks]
Name: "ffmpeg"; Description: "Install bundled FFmpeg (video transcoding and metadata)"; Components: server; Flags: checkedonce
Name: "shortcuts"; Description: "Create Desktop && Start Menu shortcuts"; Components: desktop; Flags: checkedonce

[Files]
; Server: everything from the self-contained publish output except the optional ffmpeg binaries,
; which are copied separately below only when the "ffmpeg" task is selected.
Source: "{#ServerPublishDir}\*"; DestDir: "{app}"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "ffmpeg.exe,ffprobe.exe,ffmpeg-LICENSE.txt"

Source: "{#ServerPublishDir}\ffmpeg.exe"; DestDir: "{app}"; Components: server; Tasks: ffmpeg; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#ServerPublishDir}\ffprobe.exe"; DestDir: "{app}"; Components: server; Tasks: ffmpeg; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#ServerPublishDir}\ffmpeg-LICENSE.txt"; DestDir: "{app}"; Components: server; Tasks: ffmpeg; Flags: ignoreversion skipifsourcedoesntexist

; Desktop: everything from the MauiApp self-contained publish output.
Source: "{#DesktopPublishDir}\*"; DestDir: "{app}\Desktop"; Components: desktop; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; VideoForensics.DbSetup: standalone DB create/migrate CLI tool, bundled so it can provision a
; database ahead of first service start (see deploy/windows/README.md). Installed to its own
; subfolder, not flattened into {app} alongside the server - it's an independent self-contained
; publish with its own copy of the shared-framework assemblies, and copying two separate
; self-contained outputs into the same directory risks one silently overwriting a same-named
; framework DLL from the other with a different version (this happened during development: an
; older DbSetup build shadowed the server's own Microsoft.AspNetCore.Http.Abstractions.dll and
; broke the service). Separate folders make that class of collision structurally impossible.
Source: "{#DbSetupPublishDir}\*"; DestDir: "{app}\Tools"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\VideoForensics Desktop Client"; Filename: "{app}\Desktop\VideoForensics.MauiApp.exe"; Components: desktop
Name: "{commondesktop}\VideoForensics"; Filename: "{app}\Desktop\VideoForensics.MauiApp.exe"; Components: desktop; Tasks: shortcuts

[Run]
; Service registration only runs on a genuinely fresh install - on an upgrade the service is
; already registered (stopped, not deleted, in InitializeSetup) and "sc create" would just fail.
Filename: "sc.exe"; Parameters: "create VideoForensics binPath= ""{app}\VideoForensics.WebApp.exe"" start= auto DisplayName= ""VideoForensics"""; Components: server; Check: IsFreshInstall; Flags: runhidden; StatusMsg: "Registering VideoForensics service..."
Filename: "sc.exe"; Parameters: "description VideoForensics ""VideoForensics web application and API server"""; Components: server; Check: IsFreshInstall; Flags: runhidden
Filename: "sc.exe"; Parameters: "failure VideoForensics reset= 86400 actions= restart/5000/restart/5000/restart/5000"; Components: server; Check: IsFreshInstall; Flags: runhidden
Filename: "sc.exe"; Parameters: "start VideoForensics"; Components: server; Flags: runhidden; StatusMsg: "Starting VideoForensics service..."

; First-run action: open the browser to the app on a genuinely fresh install (not an upgrade),
; so the operator lands on the first-run setup flow immediately. Assumes the default Kestrel
; port (5162, per VideoForensics.WebApp/Properties/launchSettings.json) - if the port has been
; reconfigured this simply opens the wrong URL, it does not break the install.
Filename: "{sys}\cmd.exe"; Parameters: "/c start """" ""http://localhost:5162"""; Components: server; Check: IsFreshInstall; Flags: postinstall skipifsilent runhidden nowait; Description: "Open VideoForensics in your browser"

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop VideoForensics"; Flags: runhidden; RunOnceId: "StopVideoForensicsSvc"
Filename: "sc.exe"; Parameters: "delete VideoForensics"; Flags: runhidden; RunOnceId: "DeleteVideoForensicsSvc"

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

var
  WasAlreadyInstalled: Boolean;
  DataDirPage: TInputDirWizardPage;
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
var
  ProgramDataRoot: String;
begin
  { Resolve %ProgramData%\VideoForensics to its real literal path up front (rather than leaving
    the boxes blank with just a placeholder mentioning the env var) and pre-fill every field with
    its actual resolved default - matches exactly what StorageLocationProvider.GetDefaultRoot()
    computes for each category, so an unedited box changes nothing. }
  ProgramDataRoot := ExpandConstant('{commonappdata}') + '\VideoForensics';
  DefaultDatabasePath := ProgramDataRoot;
  DefaultMediaPath := ProgramDataRoot + '\media';
  DefaultTempDownloadPath := ProgramDataRoot + '\temp';
  DefaultLogsPath := ProgramDataRoot + '\Logs';
  DefaultReportsPath := ProgramDataRoot + '\Reports';
  DefaultBackupPath := ProgramDataRoot + '\backup';

  { Optional custom data paths - separate from the install-location DirPage, one box per storage
    category (matching how the in-app Storage Settings page already treats these independently).
    Only meaningful on a fresh install: changing it on an upgrade would silently orphan existing
    data at the old location rather than moving it, so ShouldSkipPage hides this page on upgrades. }
  DataDirPage := CreateInputDirPage(wpSelectTasks,
    'Data Directory', 'Where should VideoForensics store its data?',
    'Each field defaults to a subfolder under %ProgramData%\VideoForensics. Change only the ones ' +
    'you specifically need on a different drive or path - leave the rest as they are.',
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
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (PageID = DataDirPage.ID) and (WasAlreadyInstalled or not WizardIsComponentSelected('server')) then
  begin
    Result := True;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { Event Log source registration - equivalent of WiX's never-finished util:EventSource TODO. }
    if WizardIsComponentSelected('server') then
    begin
      RegWriteStringValue(HKLM, EventLogKey, 'EventMessageFile', ExpandConstant('{app}\VideoForensics.WebApp.exe'));
      RegWriteDWordValue(HKLM, EventLogKey, 'TypesSupported', 7);
    end;

    { Custom data paths - written before the service starts (see the [Run] ordering above) so
      StorageLocationProvider.GetDefaultRoot() sees them on the very first startup. Only a field
      the user actually edited (differs from its pre-filled default) gets a registry entry - an
      untouched box should change nothing, not "helpfully" pin today's default forever. }
    if WizardIsComponentSelected('server') and IsFreshInstall then
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
