# Runs functions from the debug script with stubbed Windows Terminal calls. Start-LogViewer must open the log viewer tab in the
# named Windows Terminal window, and Get-WindowsTerminalPath must find wt.exe even when PATH does not list it.
# Run: pwsh -NoProfile -File scripts/tests/Debug-WebAppService.Tests.ps1

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '..\Debug-WebAppService.ps1'

$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)

function Import-DebugFunction {
    param([Parameter(Mandatory)][string]$Name)
    $function = $ast.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $Name
        }, $true) | Select-Object -First 1
    if (-not $function) { throw "$Name was not found in the debug script." }
    Invoke-Expression ($function.Extent.Text -replace '^function ', 'function global:')
}

$failures = @()

# Test 1: the log viewer tab targets the named window
Import-DebugFunction 'Get-WindowsTerminalPath'
Import-DebugFunction 'Start-LogViewer'
$script:launched = @()
$script:cimCalls = 0
function Get-Command { param($Name, $ErrorAction) [pscustomobject]@{ Source = 'C:\fake\wt.exe' } }
function Start-Process { param($FilePath, $ArgumentList, [switch]$PassThru) $script:launched += , @{ File = $FilePath; Args = $ArgumentList } }
function Start-Sleep { param($Seconds) }
function Write-Host { param($Object) }
function Write-Warning { param($Message) }
function Get-CimInstance {
    param($ClassName, $Filter)
    # The first query lists runs that already exist (none here); later queries find the new viewer run.
    $script:cimCalls++
    if ($script:cimCalls -eq 1) { return }
    [pscustomobject]@{ ProcessId = 4242; CommandLine = 'dotnet F:\fake\viewer-copy\VideoForensics.Utils.LogViewer.dll' }
}

$processId = Start-LogViewer -DllPath 'F:\fake\viewer-copy\VideoForensics.Utils.LogViewer.dll'
if ($script:launched.Count -ne 1) { $failures += "expected one wt.exe launch, got $($script:launched.Count)" }
else {
    $arguments = [string]$script:launched[0].Args
    if ($arguments -notmatch '-w vf-debug new-tab') {
        $failures += "log viewer tab must target the named window 'vf-debug'; got: $arguments"
    }
    # Running the published copy never builds, so the build output stays unlocked.
    if ($arguments -match '\brun\b') {
        $failures += "log viewer must run the published copy, not 'dotnet run' (which rebuilds the project); got: $arguments"
    }
    if ($arguments -notmatch 'viewer-copy') {
        $failures += "log viewer must run the copy folder, not the project's bin output; got: $arguments"
    }
}
if ($processId -ne 4242) { $failures += "expected PID 4242, got $processId" }

# Test 2: wt.exe is found in the WindowsApps folder when PATH does not list it
Remove-Item Function:\Get-Command
Import-DebugFunction 'Get-WindowsTerminalPath'
$fakeLocal = Join-Path ([System.IO.Path]::GetTempPath()) "vf-debug-test-$([guid]::NewGuid())"
$windowsApps = Join-Path $fakeLocal 'Microsoft\WindowsApps'
New-Item -ItemType Directory -Path $windowsApps -Force | Out-Null
$expected = Join-Path $windowsApps 'wt.exe'
Set-Content -Path $expected -Value ''
$savedLocal = $env:LOCALAPPDATA
$env:LOCALAPPDATA = $fakeLocal
try {
    function Get-Command { param($Name, $ErrorAction) $null }
    $found = Get-WindowsTerminalPath
    if ($found -ne $expected) { $failures += "expected wt.exe at '$expected', got '$found'" }
}
finally {
    $env:LOCALAPPDATA = $savedLocal
    Remove-Item -Recurse -Force $fakeLocal
}

# Test 3: publishing returns only the dll path. dotnet publish prints build lines, which must not reach the caller's output.
Import-DebugFunction 'Publish-LogViewer'
function dotnet {
    'MSBuild output line'
    'VideoForensics.Utils.LogViewer -> F:\fake\out\'
    $global:LASTEXITCODE = 0
}
$published = @(Publish-LogViewer -ProjectPath 'F:\fake\Viewer.csproj' -RunDirectory 'F:\fake\run')
if ($published.Count -ne 1 -or $published[0] -isnot [string]) {
    $failures += "Publish-LogViewer must return one path string, got $($published.Count) item(s)"
}
Remove-Item Function:\dotnet

if ($failures) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}
'PASS: log viewer tab targets the named window, and wt.exe is found without PATH'
