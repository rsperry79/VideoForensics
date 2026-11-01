# Checks the debug-webapp-service launch profile. Visual Studio must start Windows Terminal, not pwsh, so that Visual Studio
# does not keep a console window open for the debug script.
# Run: pwsh -NoProfile -File scripts/tests/LaunchProfile.Tests.ps1

$ErrorActionPreference = 'Stop'
$profilePath = Join-Path $PSScriptRoot '..\..\src\client\web\VideoForensics.WebApp\src\Properties\launchSettings.json'
$profile = (Get-Content -Raw -LiteralPath $profilePath | ConvertFrom-Json).profiles.'debug-webapp-service'
if (-not $profile) { throw 'The debug-webapp-service profile is missing.' }

$failures = @()
if ($profile.executablePath -ne 'wt.exe') {
    $failures += "executablePath must be wt.exe so Visual Studio has no console to keep; got '$($profile.executablePath)'"
}
if ($profile.commandLineArgs -notmatch '-w vf-debug new-tab') {
    $failures += "the tab must open in the named window 'vf-debug'; got '$($profile.commandLineArgs)'"
}
if ($profile.commandLineArgs -notmatch 'Debug-WebAppService\.ps1') {
    $failures += "the debug script must be the command; got '$($profile.commandLineArgs)'"
}

if ($failures) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}
'PASS: the debug profile starts the script in Windows Terminal'
