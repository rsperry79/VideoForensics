<#
.SYNOPSIS
Redeploys a Debug build of the VideoForensics WebApp and attaches Visual Studio to it.

.DESCRIPTION
If the "VideoForensics" Windows service is installed, the build is staged as an update with
deploy\windows\stage-update.ps1, the same path the MAUI debug launch uses. The running service applies the package
and restarts, the script waits until the service answers its health check, and the debugger of a running Visual Studio
instance is attached to the new service process. Staging needs enable-dev-updates.ps1 to have been run once as
administrator. It does not need an elevated shell.

If the service is not installed, the build is copied to -DebugDirectory (appsettings included, since no installed
configuration exists) and started as a plain process, and the debugger attaches to that process. A previous process
started from -DebugDirectory is stopped first. -Publish applies only in this case.

The attach needs an elevated Visual Studio when the service runs as LocalSystem.
Use -WhatIf to see what would happen without staging, copying, starting, or attaching.

.PARAMETER ServiceName
The Windows service that runs the WebApp.

.PARAMETER DebugDirectory
Folder the build runs from when the service is not installed.

.PARAMETER Configuration
Build configuration. Debug keeps the PDBs the debugger needs.

.PARAMETER Publish
Without an installed service, publish a self-contained build to a temporary folder instead of copying the Visual Studio
build output. Ignored when the service is installed, because staging always publishes.

.PARAMETER SkipAttach
Redeploy and start, but do not attach the debugger.

.PARAMETER ReadyTimeoutSeconds
How long to wait for the service to apply a staged build and answer its health check.

.PARAMETER NoLogConsole
Do not open the VideoForensics.Utils.Console window that shows the WebApp logger pipe output.

.PARAMETER WhatIf
Dry run: nothing is staged, copied, stopped, started, or attached.

.EXAMPLE
.\Debug-WebAppService.ps1 -WhatIf

.EXAMPLE
.\Debug-WebAppService.ps1

.EXAMPLE
.\Debug-WebAppService.ps1 -Publish
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ServiceName = 'VideoForensics',
    [string]$DebugDirectory = (Join-Path $PSScriptRoot '..\artifacts\webapp-debug'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Publish,
    [switch]$SkipAttach,
    [int]$ReadyTimeoutSeconds = 120,
    [switch]$NoLogConsole
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\client\web\VideoForensics.WebApp\src\VideoForensics.Client.WebApp.csproj'
$project = (Resolve-Path $project).Path
$stageScript = (Resolve-Path (Join-Path $PSScriptRoot '..\deploy\windows\stage-update.ps1')).Path
$updatesRoot = Join-Path $env:ProgramData 'VideoForensics\updates'
$healthUrl = 'http://localhost:5162/healthz'
$logConsoleProject = (Resolve-Path (Join-Path $PSScriptRoot '..\src\utils\console\src\VideoForensics.Utils.Console.csproj')).Path
# The build output folder is the project's bin\<Configuration>\net10.0, the folder Visual Studio builds into.
$buildOutput = Join-Path (Split-Path -Parent $project) "bin\$Configuration\net10.0"
# The published executable is named after the project's AssemblyName, not the file name.
$assemblyName = ([xml](Get-Content -Raw $project)).Project.PropertyGroup.AssemblyName |
    Where-Object { $_ } | Select-Object -First 1
$exeName = "$assemblyName.exe"

# Win32_Service.PathName is the raw command line, for example:
#   "C:\Program Files\VideoForensics\VideoForensics.WebApp.exe" --service
# The executable is quoted when its path has spaces, so both forms are handled.
function Get-ServiceExecutablePath {
    param([Parameter(Mandatory)][string]$PathName)

    if ($PathName -match '^\s*"([^"]+)"') { return $Matches[1] }
    if ($PathName -match '^\s*(.+?\.exe)(?=\s|$)') { return $Matches[1] }
    throw "Cannot read the executable from the service path: $PathName"
}

# True while a staged package is waiting for the service. Mirrors PendingUpdateProbe: a folder with a manifest.json that is
# not a staging folder (leading '.'), the backup ("previous"), or a rejected package.
function Test-PendingUpdate {
    param([Parameter(Mandatory)][string]$UpdatesRoot)

    if (-not (Test-Path $UpdatesRoot)) { return $false }
    $pending = Get-ChildItem -Path $UpdatesRoot -Directory | Where-Object {
        -not $_.Name.StartsWith('.') -and
        $_.Name -ne 'previous' -and
        -not $_.Name.EndsWith('.rejected', [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path (Join-Path $_.FullName 'manifest.json'))
    }
    return [bool]$pending
}

function Test-WebAppHealthy {
    param([Parameter(Mandatory)][string]$Url)

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

# The service is ready once the package is consumed and the health check answers. The old instance keeps answering
# until it restarts, so the staged package must be gone first.
function Wait-ForStagedBuild {
    param([Parameter(Mandatory)][int]$TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-PendingUpdate -UpdatesRoot $updatesRoot) -and (Test-WebAppHealthy -Url $healthUrl)) {
            return $true
        }
        Start-Sleep -Seconds 2
    }
    return $false
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
    # A service still pointing at an older executable name cannot take a staged update for the new name.
    if ((Split-Path -Leaf $serviceExe) -ne $exeName) {
        throw "Service $ServiceName runs $(Split-Path -Leaf $serviceExe), not $exeName. Reinstall the service with deploy\install-service.ps1, then rerun this script."
    }
    if ($WhatIfPreference) {
        Write-Host "Dry run: would stage the $Configuration build with deploy\windows\stage-update.ps1, then wait for service $ServiceName to apply it and answer $healthUrl."
        Write-Host 'Dry run only. Nothing was staged, stopped, started, or attached.'
        exit 0
    }
}
else {
    Write-Warning "Service '$ServiceName' is not installed. The build will run from $DebugDirectory instead."
    $installDirectory = $DebugDirectory
    $runExe = Join-Path $installDirectory $exeName
}

$stage = $null
if (-not $useService) {
    if ($Publish) {
        $stage = Join-Path $env:TEMP ('vf-debug-publish-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        $sourceDirectory = $stage
        Write-Host "Publishing $project ($Configuration) to $stage"
        # Self-contained, matching deploy\Deploy-WebAppToProd.ps1, so the copy has the same runtime layout.
        dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $stage
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    }
    else {
        $sourceDirectory = $buildOutput
        Write-Host "Building $project ($Configuration) into $buildOutput"
        dotnet build $project -c $Configuration
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
    }
    if (-not (Test-Path (Join-Path $sourceDirectory $exeName))) { throw "The build output has no $exeName in $sourceDirectory." }

    if ($WhatIfPreference) {
        Write-Host "Dry run: no service; the build would run from $installDirectory"
        Write-Host "Dry run only. Nothing was stopped, copied, started, or attached. Source: $sourceDirectory"
        exit 0
    }
}

try {
    # Show the WebApp logger output in its own window. The console retries until the logger's pipe appears, so it
    # can open before the WebApp starts.
    if (-not $NoLogConsole -and $PSCmdlet.ShouldProcess($logConsoleProject, 'Open logger console')) {
        Write-Host 'Opening the logger console window'
        Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $logConsoleProject) -WindowStyle Normal
    }

    $processId = $null
    if ($useService) {
        $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            # Attaching to the LocalSystem service needs elevation anyway; say so before staging rather than after.
            Write-Warning 'Not elevated: staging works, but attaching the debugger to the LocalSystem service needs an elevated Visual Studio.'
        }

        if ($PSCmdlet.ShouldProcess("service $ServiceName", "Stage the $Configuration build")) {
            Write-Host "Staging the $Configuration build with stage-update.ps1"
            & $stageScript -Build -Configuration $Configuration
            if ($LASTEXITCODE -ne 0) {
                throw "stage-update.ps1 failed with exit code $LASTEXITCODE. If access was denied, run deploy\windows\enable-dev-updates.ps1 once as administrator."
            }

            Write-Host "Waiting for service $ServiceName to apply the staged build (up to $ReadyTimeoutSeconds s)"
            if (-not (Wait-ForStagedBuild -TimeoutSeconds $ReadyTimeoutSeconds)) {
                throw "Service $ServiceName did not apply the staged build and answer $healthUrl within $ReadyTimeoutSeconds s."
            }
            Write-Host "Service $ServiceName is running the staged build."
        }

        # Read the PID after the update: the service restarted, so the process from before does not count.
        $processId = (Get-CimInstance Win32_Service -Filter "Name='$ServiceName'").ProcessId
        if ($processId -le 0) { throw "Service $ServiceName reports no process ID after the update." }
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

        if ($PSCmdlet.ShouldProcess($installDirectory, 'Copy build')) {
            Write-Host "Copying build to $installDirectory"
            # robocopy exit codes 0-7 mean success; 8 and above mean a copy failed.
            & robocopy $sourceDirectory $installDirectory /E /NP /NDL
            if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }
        }

        if ($PSCmdlet.ShouldProcess($runExe, 'Start')) {
            Write-Host "Starting $runExe"
            $processId = (Start-Process -FilePath $runExe -WorkingDirectory $installDirectory -PassThru).Id
        }
    }

    if (-not $SkipAttach -and $processId) {
        Attach-VisualStudioDebugger -ProcessId $processId
    }
}
finally {
    if ($stage) {
        Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
    }
}
