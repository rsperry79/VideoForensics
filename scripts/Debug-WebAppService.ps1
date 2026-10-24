<#
.SYNOPSIS
Redeploys a Debug build of the VideoForensics WebApp and attaches Visual Studio to it.

.DESCRIPTION
If the "VideoForensics" Windows service is installed, its install folder is read from the service's
executable path. The script publishes a Debug build to a temporary folder, stops the service if it is
running, copies the build into the service folder, starts the service, and attaches the debugger of a
running Visual Studio instance to the service process.

If the service is not installed, the build is copied to -DebugDirectory (appsettings included, since no
installed configuration exists) and started as a plain process, and the debugger attaches to that process.
A previous process started from -DebugDirectory is stopped first.

For an installed service, appsettings.json and appsettings.Development.json are not copied unless
-IncludeAppSettings is given, so the installed configuration is kept. Files already in the install folder
that the build does not produce are kept (no /MIR).

Installing the service is done with deploy\install-service.ps1. Redeploying an installed service needs an
elevated PowerShell, and the attach needs an elevated Visual Studio because the service runs as LocalSystem.
Use -WhatIf to publish and list the copy without stopping, copying, starting, or attaching.

.PARAMETER ServiceName
The Windows service that runs the WebApp.

.PARAMETER DebugDirectory
Folder the build runs from when the service is not installed.

.PARAMETER Configuration
Build configuration to publish. Debug keeps the PDBs the debugger needs.

.PARAMETER IncludeAppSettings
Also copy appsettings.json and appsettings.Development.json from the build when a service is installed.

.PARAMETER SkipAttach
Redeploy and start, but do not attach the debugger.

.PARAMETER WhatIf
Dry run: publish to a temp folder and list what would be copied.

.EXAMPLE
.\Debug-WebAppService.ps1 -WhatIf

.EXAMPLE
.\Debug-WebAppService.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ServiceName = 'VideoForensics',
    [string]$DebugDirectory = (Join-Path $PSScriptRoot '..\artifacts\webapp-debug'),
    [string]$Configuration = 'Debug',
    [switch]$IncludeAppSettings,
    [switch]$SkipAttach
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\client\web\VideoForensics.WebApp\src\VideoForensics.Client.WebApp.csproj'
$project = (Resolve-Path $project).Path
# The published executable is named after the project's AssemblyName, not the file name.
$assemblyName = ([xml](Get-Content -Raw $project)).Project.PropertyGroup.AssemblyName |
    Where-Object { $_ } | Select-Object -First 1
$exeName = "$assemblyName.exe"
$stage = Join-Path $env:TEMP ('vf-debug-publish-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))

# Win32_Service.PathName is the raw command line, for example:
#   "C:\Program Files\VideoForensics\VideoForensics.WebApp.exe" --service
# The executable is quoted when its path has spaces, so both forms are handled.
function Get-ServiceExecutablePath {
    param([Parameter(Mandatory)][string]$PathName)

    if ($PathName -match '^\s*"([^"]+)"') { return $Matches[1] }
    if ($PathName -match '^\s*(.+?\.exe)(?=\s|$)') { return $Matches[1] }
    throw "Cannot read the executable from the service path: $PathName"
}

# Attaches the debugger of a running Visual Studio instance. Without an open instance the PID is reported
# so it can be attached by hand.
function Attach-VisualStudioDebugger {
    param([Parameter(Mandatory)][int]$ProcessId)

    try {
        $dte = [System.Runtime.InteropServices.Marshal]::GetActiveObject('VisualStudio.DTE')
    }
    catch {
        Write-Warning "No running Visual Studio instance found. Attach to PID $ProcessId manually."
        return
    }

    $target = $dte.Debugger.LocalProcesses | Where-Object { $_.ProcessID -eq $ProcessId } | Select-Object -First 1
    if (-not $target) {
        throw "PID $ProcessId is not in the Visual Studio process list. Check that Visual Studio runs elevated for a service."
    }

    $target.Attach()
    Write-Host "Visual Studio attached to $($target.Name) (PID $ProcessId)."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$serviceInfo = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
$useService = [bool]($service -and $serviceInfo)

if ($useService) {
    $serviceExe = Get-ServiceExecutablePath -PathName $serviceInfo.PathName
    $installDirectory = Split-Path -Parent $serviceExe
    # The service still points at an older executable name (renamed assembly). Its path is repointed to
    # the new name before the copy; otherwise the service would keep running the old file.
    $repointService = (Split-Path -Leaf $serviceExe) -ne $exeName
}
else {
    Write-Warning "Service '$ServiceName' is not installed. The build will run from $DebugDirectory instead."
    $installDirectory = $DebugDirectory
}
$runExe = Join-Path $installDirectory $exeName

Write-Host "Publishing $project ($Configuration) to $stage"
# Self-contained, matching deploy\Deploy-WebAppToProd.ps1, so the copy has the same runtime layout.
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
if (-not (Test-Path (Join-Path $stage $exeName))) { throw "Publish did not produce $exeName." }

# robocopy exit codes 0-7 mean success; 8 and above mean a copy failed.
$robocopyArgs = @($stage, $installDirectory, '/E', '/NP', '/NDL')
if ($useService -and -not $IncludeAppSettings) {
    $robocopyArgs += @('/XF', 'appsettings.json', 'appsettings.Development.json')
}

if ($WhatIfPreference) {
    if ($useService) {
        Write-Host "Dry run: service $ServiceName runs from $installDirectory"
        if ($repointService) { Write-Host "Dry run: service path would change to $exeName" }
    }
    else { Write-Host "Dry run: no service; the build would run from $installDirectory" }
    Write-Host "Dry run: files that would be copied to $installDirectory"
    & robocopy @robocopyArgs /L
    Write-Host "Dry run only. Nothing was stopped, copied, started, or attached. Staged build: $stage"
    # robocopy /L returns 1-3 for a normal listing; that is not a failure here.
    exit 0
}

try {
    if ($useService) {
        $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw 'Run this script from an elevated PowerShell (Run as administrator).'
        }

        if ($service.Status -eq 'Running') {
            if ($PSCmdlet.ShouldProcess("service $ServiceName", 'Stop')) {
                Write-Host "Stopping service $ServiceName"
                Stop-Service -Name $ServiceName -Force
                $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
            }
        }
        else {
            Write-Host "Service $ServiceName is $($service.Status); nothing to stop."
        }

        if ($repointService -and $PSCmdlet.ShouldProcess("service $ServiceName", "Point to $exeName")) {
            $newPath = Join-Path $installDirectory $exeName
            Write-Host "Pointing service $ServiceName at $newPath"
            $result = Invoke-CimMethod -InputObject $serviceInfo -MethodName Change -Arguments @{ PathName = "`"$newPath`"" }
            if ($result.ReturnValue -ne 0) { throw "Changing the service path failed with code $($result.ReturnValue)." }
        }
    }
    else {
        # Stop only the copy this script started earlier, matched by its exact executable path.
        $previous = @(Get-Process -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -eq $runExe })
        foreach ($process in $previous) {
            if ($PSCmdlet.ShouldProcess("PID $($process.Id)", 'Stop previous debug run')) {
                Write-Host "Stopping previous debug run (PID $($process.Id))"
                Stop-Process -Id $process.Id -Force
                $process.WaitForExit(60000) | Out-Null
            }
        }
    }

    if ($PSCmdlet.ShouldProcess($installDirectory, 'Copy build')) {
        Write-Host "Copying build to $installDirectory"
        & robocopy @robocopyArgs
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }
    }

    if ($useService) {
        if ($PSCmdlet.ShouldProcess("service $ServiceName", 'Start')) {
            Write-Host "Starting service $ServiceName"
            Start-Service -Name $ServiceName
            $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
            Write-Host "Service $ServiceName is running."
        }
        # Read the PID after start: the service process did not exist before this run.
        $processId = (Get-CimInstance Win32_Service -Filter "Name='$ServiceName'").ProcessId
        if ($processId -le 0) { throw "Service $ServiceName reports no process ID after start." }
    }
    elseif ($PSCmdlet.ShouldProcess($runExe, 'Start')) {
        Write-Host "Starting $runExe"
        $processId = (Start-Process -FilePath $runExe -WorkingDirectory $installDirectory -PassThru).Id
    }

    if (-not $SkipAttach -and $processId) {
        Attach-VisualStudioDebugger -ProcessId $processId
    }
}
finally {
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
}
