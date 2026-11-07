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
Without an installed service, publish a framework-dependent build to a temporary folder instead of copying the Visual Studio
build output. Ignored when the service is installed, because staging always publishes.

.PARAMETER SkipAttach
Redeploy and start, but do not attach the debugger.

.PARAMETER ReadyTimeoutSeconds
How long to wait for the service to apply a staged build and answer its health check.

.PARAMETER NoLogConsole
Do not open the VideoForensics.Utils.LogViewer window that shows the WebApp logger pipe output.

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
    [string]$DebugDirectory,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Publish,
    [switch]$SkipAttach,
    # The service drains in-flight work before it applies a staged build, which can take minutes.
    [int]$ReadyTimeoutSeconds = 300,
    [switch]$NoLogConsole
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 leaves $PSScriptRoot empty in parameter defaults under -File, so the default is set here.
if (-not $DebugDirectory) { $DebugDirectory = Join-Path $PSScriptRoot '..\..\..\..\..\artifacts\webapp-debug' }

$project = Join-Path $PSScriptRoot '..\..\..\..\..\src\client\web\VideoForensics.WebApp\src\VideoForensics.Client.WebApp.csproj'
$project = (Resolve-Path $project).Path
$updatesRoot = Join-Path $env:ProgramData 'VideoForensics\updates'
$healthUrl = 'http://localhost:5162/healthz'
$logConsoleProject = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..\src\utils\logviewer\src\VideoForensics.Utils.LogViewer.csproj')).Path
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

# The process ID that listens on the WebApp port, or $null when nothing listens there.
function Get-ListeningProcessId {
    param([Parameter(Mandatory)][int]$Port)

    $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($listener) { return [int]$listener.OwningProcess }
    return $null
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

# Returns the running Visual Studio instance, or null. Marshal.GetActiveObject exists only in Windows PowerShell 5.1,
# so PowerShell 7 calls oleaut32 directly.
function Get-RunningVisualStudio {
    if (-not ('VideoForensics.Ole' -as [type])) {
        Add-Type -Namespace VideoForensics -Name Ole -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("oleaut32.dll", PreserveSig = false)]
public static extern void GetActiveObject(ref System.Guid rclsid, System.IntPtr pvReserved, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Interface)] out object ppunk);
'@
    }

    try {
        $clsid = [Type]::GetTypeFromProgID('VisualStudio.DTE').GUID
        $instance = $null
        [VideoForensics.Ole]::GetActiveObject([ref]$clsid, [IntPtr]::Zero, [ref]$instance)
        return $instance
    }
    catch {
        return $null
    }
}

# Builds the solution in the running Visual Studio, the way F5 would. It runs only when the open solution is this
# checkout and its active configuration is the one asked for, so Visual Studio's settings are never changed. Returns
# $false when it did not apply, and the caller falls back to dotnet build.
function Build-WithVisualStudio {
    param([Parameter(Mandatory)][string]$Configuration)

    $dte = Get-RunningVisualStudio
    if (-not $dte) { return $false }

    $solutionPath = $dte.Solution.FullName
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path
    if (-not $solutionPath -or (Split-Path -Parent $solutionPath) -ne $repoRoot) {
        Write-Warning "The open Visual Studio solution is not this checkout ($solutionPath). Building with dotnet build."
        return $false
    }
    $activeConfiguration = $dte.Solution.SolutionBuild.ActiveConfiguration.Name
    if ($activeConfiguration -ne $Configuration) {
        Write-Warning "Visual Studio's active configuration is $activeConfiguration, not $Configuration. Building with dotnet build."
        return $false
    }

    Write-Host "Building the solution in Visual Studio ($Configuration)"
    $dte.Solution.SolutionBuild.Build($true)
    $failed = $dte.Solution.SolutionBuild.LastBuildInfo
    if ($failed -ne 0) { throw "Visual Studio build failed in $failed project(s). See its Error List." }
    return $true
}

# Returns the full path of wt.exe, or $null when Windows Terminal is not installed. The WindowsApps folder is checked
# directly, because PATH in a process that Visual Studio starts does not always list it.
function Get-WindowsTerminalPath {
    $command = Get-Command wt.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\wt.exe'
    if (Test-Path -LiteralPath $alias) { return $alias }
    return $null
}

# Publishes the log viewer into a new folder for this run and returns the path of its dll. The viewer runs from that copy, so
# its build output in bin is never locked by a viewer that is still open, and a new folder per run avoids overwriting one.
function Publish-LogViewer {
    param([Parameter(Mandatory)][string]$ProjectPath, [Parameter(Mandatory)][string]$RunDirectory)

    $target = Join-Path $RunDirectory "logviewer-$(Get-Date -Format 'yyyyMMddHHmmss')"
    Write-Host "Publishing the log viewer to $target"
    # Out-Host keeps the build lines on screen without adding them to the output, which must be only the dll path.
    dotnet publish $ProjectPath -c Debug -o $target | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish of the log viewer failed with exit code $LASTEXITCODE." }
    return (Join-Path $target "$([System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)).dll")
}

# Opens the log viewer as a tab in Windows Terminal when wt.exe is installed, otherwise in its own window. The viewer runs
# the published dll with dotnet, not dotnet run, so the launch never builds. Windows Terminal returns at once, so the
# viewer's process is found by its command line. Returns that process ID, or $null when it cannot be found.
function Start-LogViewer {
    param([Parameter(Mandatory)][string]$DllPath)

    $wt = Get-WindowsTerminalPath
    if (-not $wt) {
        Write-Host 'Windows Terminal is not installed. Opening the logger console in its own window.'
        return (Start-Process -FilePath 'dotnet' -ArgumentList @($DllPath) -WindowStyle Normal -PassThru).Id
    }

    # Runs that were already open are excluded, so the new tab's process is the one matched.
    $existing = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($DllPath) } |
        ForEach-Object { $_.ProcessId })

    Write-Host 'Opening the logger console as a Windows Terminal tab'
    Start-Process -FilePath $wt -ArgumentList "-w vf-debug new-tab --title `"Log viewer`" dotnet `"$DllPath`"" | Out-Null

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $match = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
            Where-Object { $_.CommandLine -and $_.CommandLine.Contains($DllPath) -and $_.ProcessId -notin $existing } |
            Select-Object -First 1
        if ($match) { return [int]$match.ProcessId }
        Start-Sleep -Seconds 1
    }
    Write-Warning 'The logger console did not start in Windows Terminal within 30 s. Closing it will not stop the debugger.'
    return $null
}

# Attaches the debugger of a running Visual Studio instance. Without an open instance the PID is reported
# so it can be attached by hand.
function Attach-VisualStudioDebugger {
    param([Parameter(Mandatory)][int]$ProcessId)

    $dte = Get-RunningVisualStudio
    if (-not $dte) {
        Write-Warning "No running Visual Studio instance found. Attach to PID $ProcessId manually."
        return
    }

    $target = $dte.Debugger.LocalProcesses | Where-Object { $_.ProcessID -eq $ProcessId } | Select-Object -First 1
    if (-not $target) {
        throw "PID $ProcessId is not in the Visual Studio process list. Check that Visual Studio runs elevated for a service."
    }

    $target.Attach()
    Write-Host "Visual Studio attached to $($target.Name) (PID $ProcessId)."
    return $dte
}

# The debug build is framework-dependent, so the x64 .NET 10 runtime must be installed. The service finds its runtime
# through the x64 InstalledVersions registry key rather than PATH, so that key is what gets checked.
function Test-DotNet10Runtime {
    foreach ($framework in 'Microsoft.NETCore.App', 'Microsoft.AspNetCore.App') {
        $key = "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\$framework"
        $versions = if (Test-Path $key) { @((Get-Item $key).GetValueNames()) } else { @() }
        if (-not ($versions | Where-Object { $_ -like '10.*' })) {
            throw "$framework 10.x (x64) is not installed. Install the .NET 10 ASP.NET Core Runtime (x64) from https://dot.net, then rerun this script."
        }
    }
    Write-Host '.NET 10 runtime (x64) is installed.'
}

Test-DotNet10Runtime

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
        Write-Host "Dry run: would wait for service $ServiceName to apply any staged build and answer $healthUrl. Visual Studio stages Debug builds."
        Write-Host 'Dry run only. Nothing was staged, stopped, started, or attached.'
        exit 0
    }

    # Attaching to the LocalSystem service needs an elevated debugger, and DTE only lists Visual Studio at the same
    # integrity level. Staging alone does not need elevation, so only an attach triggers the UAC prompt. It runs before
    # anything is staged, so a declined prompt changes nothing.
    $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $SkipAttach -and -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host 'Attaching to the LocalSystem service needs elevation. Relaunching this script elevated (UAC prompt).'
        $relaunchArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
        foreach ($name in $PSBoundParameters.Keys) {
            if ($name -in 'WhatIf', 'Confirm') { continue }
            $value = $PSBoundParameters[$name]
            if ($value -is [switch]) {
                if ($value.IsPresent) { $relaunchArguments += "-$name" }
            }
            else {
                $relaunchArguments += "-$name", "`"$value`""
            }
        }

        $shell = (Get-Process -Id $PID).Path
        $wt = Get-WindowsTerminalPath
        try {
            if ($wt) {
                # One elevated Windows Terminal window holds the script and the log viewer. Windows Terminal returns at once,
                # so this shell exits without waiting and the script's own exit code is not passed back.
                $tabCommand = "`"$shell`" " + ($relaunchArguments -join " ")
                Start-Process -FilePath $wt -ArgumentList "-w vf-debug new-tab --title `"VideoForensics debug`" $tabCommand" -Verb RunAs | Out-Null
                exit 0
            }
            $elevated = Start-Process -FilePath $shell -ArgumentList $relaunchArguments -Verb RunAs -Wait -PassThru
        }
        catch {
            throw "Elevation was declined or failed, so nothing was staged or attached. $($_.Exception.Message)"
        }
        exit $elevated.ExitCode
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
        # Framework-dependent: the machine's .NET 10 runtime runs it, the same build the service path stages.
        dotnet publish $project -c $Configuration -r win-x64 --self-contained false -o $stage
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    }
    else {
        $sourceDirectory = $buildOutput
        $builtByVisualStudio = -not $WhatIfPreference -and (Build-WithVisualStudio -Configuration $Configuration)
        if (-not $builtByVisualStudio) {
            Write-Host "Building $project ($Configuration) into $buildOutput"
            dotnet build $project -c $Configuration
            if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
        }
    }
    if (-not (Test-Path (Join-Path $sourceDirectory $exeName))) { throw "The build output has no $exeName in $sourceDirectory." }

    if ($WhatIfPreference) {
        Write-Host "Dry run: no service; the build would run from $installDirectory"
        Write-Host "Dry run only. Nothing was stopped, copied, started, or attached. Source: $sourceDirectory"
        exit 0
    }
}

try {
    # Show the WebApp logger output in a log viewer. The console retries until the logger's pipe appears, so it can
    # open before the WebApp starts.
    $viewerId = $null
    if (-not $NoLogConsole -and $PSCmdlet.ShouldProcess($logConsoleProject, 'Open logger console')) {
        $viewerDll = Publish-LogViewer -ProjectPath $logConsoleProject -RunDirectory $DebugDirectory
        $viewerId = Start-LogViewer -DllPath $viewerDll
    }

    $processId = $null
    if ($useService) {

        # The server is started only when it is not already answering. A stopped service is started here, then the
        # wait below confirms it answers.
        if (-not (Test-WebAppHealthy -Url $healthUrl) -and $service.Status -ne 'Running' -and
            $PSCmdlet.ShouldProcess("service $ServiceName", 'Start')) {
            Write-Host "Starting service $ServiceName (it is not answering $healthUrl)"
            Start-Service -Name $ServiceName
        }

        # Visual Studio stages each Debug build (deploy\windows\stage-debug-build.ps1). Here the script waits for the
        # service to apply that build, so build in Visual Studio first.
        if ($PSCmdlet.ShouldProcess("service $ServiceName", "Wait for the service to run the current build")) {
            Write-Host "Waiting for service $ServiceName to apply any staged build (up to $ReadyTimeoutSeconds s)"
            if (-not (Wait-ForStagedBuild -TimeoutSeconds $ReadyTimeoutSeconds)) {
                throw "Service $ServiceName did not apply the staged build and answer $healthUrl within $ReadyTimeoutSeconds s."
            }
            Write-Host "Service $ServiceName is running the staged build."
        }

        # Read the PID after the update: the service restarted, so the process from before does not count.
        $processId = (Get-CimInstance Win32_Service -Filter "Name='$ServiceName'").ProcessId
        if ($processId -le 0) { throw "Service $ServiceName reports no process ID after the update." }
    }
    elseif (Test-WebAppHealthy -Url $healthUrl) {
        # The server already answers, so it is not started again. Attach to the process that owns its port.
        $processId = Get-ListeningProcessId -Port 5162
        Write-Host "The server already answers $healthUrl (PID $processId). Not starting another copy."
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
        Attach-VisualStudioDebugger -ProcessId $processId | Out-Null
    }
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ([Environment]::UserInteractive) { Read-Host 'Press Enter to close this window' | Out-Null }
}
finally {
    if ($stage) {
        Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
    }
}
