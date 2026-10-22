<#
.SYNOPSIS
Publishes the VideoForensics WebApp and copies it over the production install.

.DESCRIPTION
Publishes a Release build to a temporary folder, stops the "VideoForensics" Windows service,
copies the build into the install folder, and starts the service again.

appsettings.json and appsettings.Development.json are never copied. Production keeps its own
configuration, so any new setting a build needs must be added to the production file by hand.
Files already in the install folder that the build does not produce are kept (no /MIR).

Run from an elevated PowerShell. Use -WhatIf to publish and list the copy without stopping the
service or changing the install folder.

.PARAMETER Destination
The production install folder.

.PARAMETER ServiceName
The Windows service that runs the WebApp.

.PARAMETER WhatIf
Dry run: publish to a temp folder and list what would be copied.

.EXAMPLE
.\Deploy-WebAppToProd.ps1 -WhatIf

.EXAMPLE
.\Deploy-WebAppToProd.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Destination = 'C:\Program Files\VideoForensics',
    [string]$ServiceName = 'VideoForensics',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\src\client\web\VideoForensics.WebApp\src\VideoForensics.WebApp.csproj'
$project = (Resolve-Path $project).Path
$stage = Join-Path $env:TEMP ('vf-publish-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$exeName = 'VideoForensics.WebApp.exe'

Write-Host "Publishing $project ($Configuration) to $stage"
dotnet publish $project -c $Configuration -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
if (-not (Test-Path (Join-Path $stage $exeName))) { throw "Publish did not produce $exeName." }

# robocopy exit codes 0-7 mean success; 8 and above mean a copy failed.
$robocopyArgs = @(
    $stage, $Destination,
    '/E',
    '/XF', 'appsettings.json', 'appsettings.Development.json',
    '/NP', '/NDL'
)

if ($WhatIfPreference) {
    Write-Host "Dry run: files that would be copied to $Destination"
    & robocopy @robocopyArgs /L
    Write-Host "Dry run only. Nothing was stopped, copied, or started. Staged build: $stage"
    $LASTEXITCODE = 0
    return
}

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell (Run as administrator).'
}

$service = Get-Service -Name $ServiceName -ErrorAction Stop
if ($PSCmdlet.ShouldProcess("service $ServiceName", 'Stop')) {
    if ($service.Status -eq 'Running') {
        Write-Host "Stopping service $ServiceName"
        Stop-Service -Name $ServiceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
    }
}

if ($PSCmdlet.ShouldProcess($Destination, 'Copy build')) {
    Write-Host "Copying build to $Destination"
    & robocopy @robocopyArgs
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE. The service is still stopped." }
}

if ($PSCmdlet.ShouldProcess("service $ServiceName", 'Start')) {
    Write-Host "Starting service $ServiceName"
    Start-Service -Name $ServiceName
    $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
    Write-Host "Service $ServiceName is running."
}

Remove-Item -Recurse -Force $stage
