#Requires -RunAsAdministrator
<#
.SYNOPSIS
    One-time setup so stage-update.ps1 (and Visual Studio F5) can update the installed service without administrator rights.

.DESCRIPTION
    Sets service-scoped environment variables (Updates__Enabled, Updates__AllowDevPackages, Updates__DevWriterUser) in the
    service's registry key, then restarts the service. On start the service locks the updates folder to SYSTEM and
    Administrators plus the named developer account, and watches it for staged packages.

    Dev packages are unsigned and may reuse the running version, so this must only be run on a development machine.
    Run disable-dev-updates by passing -Disable. Environment variables set here survive updates, which never touch the registry.

.PARAMETER DevUser
    Account allowed to stage packages, as DOMAIN\user. Defaults to the current user.

.PARAMETER Disable
    Remove the settings and restart the service.
#>
[CmdletBinding()]
param(
    [string]$DevUser = "$env:USERDOMAIN\$env:USERNAME",

    [switch]$Disable
)

$ErrorActionPreference = 'Stop'
$ServiceName = 'VideoForensics'
$key = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$managed = 'Updates__Enabled', 'Updates__AllowDevPackages', 'Updates__DevWriterUser'

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    throw "Service '$ServiceName' is not installed. Run the installer once first."
}

# Keep any other variables already set on the service; replace only the ones this script manages.
$current = @((Get-ItemProperty -Path $key -Name Environment -ErrorAction SilentlyContinue).Environment) | Where-Object { $_ }
$kept = $current | Where-Object { $name = ($_ -split '=', 2)[0]; $managed -notcontains $name }

if ($Disable) {
    $values = @($kept)
}
else {
    $values = @($kept) + @('Updates__Enabled=true', 'Updates__AllowDevPackages=true', "Updates__DevWriterUser=$DevUser")
}

if ($values.Count -gt 0) {
    Set-ItemProperty -Path $key -Name Environment -Value ([string[]]$values) -Type MultiString
}
else {
    Remove-ItemProperty -Path $key -Name Environment -ErrorAction SilentlyContinue
}

Restart-Service -Name $ServiceName -Force
if ($Disable) {
    Write-Host 'Dev updates disabled; service restarted.' -ForegroundColor Green
}
else {
    Write-Host "Dev updates enabled for $DevUser; service restarted. Run deploy\windows\stage-update.ps1 -Build to stage the current source." -ForegroundColor Green
}
