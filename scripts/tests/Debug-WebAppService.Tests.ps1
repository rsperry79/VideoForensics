# Runs Start-LogViewer from the debug script with stubbed Windows Terminal calls and checks that the log viewer tab opens in
# the same named Windows Terminal window as the debug script, so one window holds both.
# Run: pwsh -NoProfile -File scripts/tests/Debug-WebAppService.Tests.ps1

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '..\Debug-WebAppService.ps1'

$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
$function = $ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Start-LogViewer'
    }, $true) | Select-Object -First 1
if (-not $function) { throw 'Start-LogViewer was not found in the debug script.' }
Invoke-Expression $function.Extent.Text

$script:launched = @()
function Get-Command { param($Name, $ErrorAction) [pscustomobject]@{ Source = 'C:\fake\wt.exe' } }
function Start-Process { param($FilePath, $ArgumentList, [switch]$PassThru) $script:launched += , @{ File = $FilePath; Args = $ArgumentList } }
function Start-Sleep { param($Seconds) }
function Write-Host { param($Object) }
function Write-Warning { param($Message) }
$script:cimCalls = 0
function Get-CimInstance {
    param($ClassName, $Filter)
    # The first query lists runs that already exist (none here); later queries find the new viewer run.
    $script:cimCalls++
    if ($script:cimCalls -eq 1) { return }
    [pscustomobject]@{ ProcessId = 4242; CommandLine = 'dotnet run --project F:\fake\Viewer.csproj' }
}

$projectPath = 'F:\fake\Viewer.csproj'
$processId = Start-LogViewer -ProjectPath $projectPath

$failures = @()
if ($script:launched.Count -ne 1) { $failures += "expected one wt.exe launch, got $($script:launched.Count)" }
else {
    $arguments = [string]$script:launched[0].Args
    if ($arguments -notmatch '-w vf-debug new-tab') {
        $failures += "log viewer tab must target the named window 'vf-debug'; got: $arguments"
    }
}
if ($processId -ne 4242) { $failures += "expected PID 4242, got $processId" }

if ($failures) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}
'PASS: log viewer tab opens in the named Windows Terminal window'
