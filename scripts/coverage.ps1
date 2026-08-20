# Run tests with code coverage for the Ring API

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootPath = Split-Path -Parent $scriptPath
$solutionFile = "$rootPath\src\Ring.Api.sln"
$resultsDir = "$rootPath\TestResults"

Write-Host "Running code coverage for Ring API..." -ForegroundColor Cyan

# Clean previous results
if (Test-Path $resultsDir) {
    Remove-Item $resultsDir -Recurse -Force
}

# Run all tests with coverage collection
dotnet test $solutionFile `
    --configuration Release `
    --logger trx `
    --collect:"XPlat Code Coverage" `
    --settings "$projectPath\.runsettings" `
    -- RunConfiguration.DisableAppDomain=true

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nTests completed successfully!" -ForegroundColor Green

    # Generate HTML report if ReportGenerator is available
    $coverageFile = Get-ChildItem "$resultsDir" -Filter "coverage.opencover.xml" -Recurse | Select-Object -First 1

    if ($coverageFile) {
        Write-Host "`nGenerating HTML coverage report..." -ForegroundColor Cyan
        $reportDir = "$resultsDir\Coverage"

        dotnet tool list -g | Select-String "reportgenerator" | Out-Null
        if ($?) {
            reportgenerator -reports:"$($coverageFile.FullName)" -targetdir:"$reportDir" -reporttypes:HtmlInline_AzurePipelines
            Write-Host "Coverage report generated at: $reportDir" -ForegroundColor Green
            Write-Host "Open: $reportDir\index.html" -ForegroundColor Cyan
        }
        else {
            Write-Host "ReportGenerator not found. Install it with: dotnet tool install -g dotnet-reportgenerator-globaltool" -ForegroundColor Yellow
        }
    }
}
else {
    Write-Host "`nTests failed!" -ForegroundColor Red
    exit 1
}
