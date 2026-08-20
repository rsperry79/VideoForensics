#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Sets up and runs Ring API tests with optional credential configuration.

.DESCRIPTION
    Complete test setup utility for the Ring API:
    - Builds test project in Debug configuration
    - Optionally sets up Ring API credentials for real integration tests
    - Runs all tests (mock + real + legacy)
    - Generates coverage report

.PARAMETER SetupCredentials
    Prompt to set up Ring API credentials for real integration tests.
    Accepts: -SetupCredentials (interactive) or -SetupCredentials "email@example.com" "password"

.PARAMETER Email
    Ring API email (used with -SetupCredentials)

.PARAMETER Password
    Ring API password (used with -SetupCredentials)

.PARAMETER GenerateCoverage
    Generate HTML coverage report after running tests (default: true)

.PARAMETER NoCoverage
    Skip coverage report generation

.PARAMETER BuildOnly
    Only build the test project, don't run tests

.PARAMETER Verbose
    Show detailed build and test output

.EXAMPLE
    # Run tests (mock only, no setup needed)
    .\setup-tests.ps1

.EXAMPLE
    # Setup credentials and run tests
    .\setup-tests.ps1 -SetupCredentials

.EXAMPLE
    # Setup credentials non-interactively
    .\setup-tests.ps1 -SetupCredentials -Email "test@example.com" -Password "password"

.EXAMPLE
    # Only build, don't run tests
    .\setup-tests.ps1 -BuildOnly

.EXAMPLE
    # Run tests without coverage report
    .\setup-tests.ps1 -NoCoverage
#>

param(
    [switch]$SetupCredentials,
    [string]$Email,
    [string]$Password,
    [switch]$GenerateCoverage = $true,
    [switch]$NoCoverage,
    [switch]$BuildOnly,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Helper functions
function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host ">>> $Message <<<" -ForegroundColor Magenta
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

# Main script starts here
Write-Header "Ring API Test Setup"

# Get current directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$SolutionFile = Join-Path $RootDir "src\Ring.Api.sln"

# Verify solution file exists
if (-not (Test-Path $SolutionFile)) {
    Write-Error-Custom "Solution file not found: $SolutionFile"
    exit 1
}

Write-Info "Location: $RootDir"
Write-Info "Solution File: $SolutionFile"
Write-Host ""

# Step 1: Build
Write-Header "Step 1: Building Test Project"

$BuildArgs = @(
    "build",
    $SolutionFile,
    "-c", "Debug"
)

if (-not $Verbose) {
    $BuildArgs += "-v", "minimal"
}

try {
    & dotnet $BuildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Custom "Build failed with exit code $LASTEXITCODE"
        exit 1
    }
    Write-Success "Build completed"
}
catch {
    Write-Error-Custom "Build error: $_"
    exit 1
}

# Exit early if BuildOnly flag is set
if ($BuildOnly) {
    Write-Host ""
    Write-Success "Build completed successfully!"
    exit 0
}

# Step 2: Setup credentials if requested
if ($SetupCredentials) {
    Write-Header "Step 2: Setting Up Ring API Credentials"

    if (-not (Test-Path $ApiTesterProject)) {
        Write-Error-Custom "ApiTester project not found: $ApiTesterProject"
        exit 1
    }

    # Check if email and password were provided
    if ([string]::IsNullOrWhiteSpace($Email) -or [string]::IsNullOrWhiteSpace($Password)) {
        Write-Info "Running ApiTester --auth in interactive mode..."
        Write-Info "Enter your Ring API credentials when prompted"
        Write-Host ""

        try {
            & dotnet run --project $ApiTesterProject -- --auth
            if ($LASTEXITCODE -ne 0) {
                Write-Error-Custom "Credentials setup failed"
                exit 1
            }
        }
        catch {
            Write-Error-Custom "Setup error: $_"
            exit 1
        }
    }
    else {
        Write-Info "Using provided credentials..."
        try {
            & dotnet run --project $ApiTesterProject -- --auth --username $Email --password $Password
            if ($LASTEXITCODE -ne 0) {
                Write-Error-Custom "Credentials setup failed"
                exit 1
            }
        }
        catch {
            Write-Error-Custom "Setup error: $_"
            exit 1
        }
    }

    Write-Host ""
}
else {
    Write-Header "Step 2: Credentials"
    Write-Info "Skipped (use -SetupCredentials to configure)"
    Write-Host ""
}

# Step 3: Run tests
Write-Header "Step 3: Running Tests"

$TestArgs = @(
    "test",
    $SolutionFile,
    "-c", "Debug",
    "--collect:XPlat Code Coverage",
    "--settings", (Join-Path $RootDir "src\.runsettings")
)

if (-not $Verbose) {
    $TestArgs += "-v", "minimal"
}

try {
    Write-Info "Running tests (this may take a moment)..."
    & dotnet $TestArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Success "All tests passed!"
    }
    else {
        Write-Warning-Custom "Some tests failed or were inconclusive (exit code: $LASTEXITCODE)"
    }
}
catch {
    Write-Error-Custom "Test execution error: $_"
    exit 1
}

Write-Host ""

# Step 4: Generate coverage report
if (-not $NoCoverage -and $GenerateCoverage) {
    Write-Header "Step 4: Generating Coverage Report"

    $CoverageScript = Join-Path $ScriptDir "coverage.ps1"

    if (Test-Path $CoverageScript) {
        try {
            Write-Info "Generating HTML coverage report..."
            & $CoverageScript

            $CoverageReport = Join-Path $RootDir "TestResults\Coverage\index.html"
            if (Test-Path $CoverageReport) {
                Write-Success "Coverage report generated: $CoverageReport"
                Write-Info "Opening report in browser..."
                Start-Process $CoverageReport
            }
            else {
                Write-Warning-Custom "Coverage report file not found"
            }
        }
        catch {
            Write-Warning-Custom "Coverage generation error: $_"
        }
    }
    else {
        Write-Warning-Custom "Coverage script not found: $CoverageScript"
    }
}
else {
    Write-Header "Step 4: Coverage Report"
    Write-Info "Skipped (use -NoCoverage to disable)"
}

Write-Host ""
Write-Header "Test Setup Complete!"
Write-Success "All steps completed successfully!"

Write-Host ""
Write-Info "Next steps:"
Write-Host "  * Mock tests: Run without credentials (already tested)"
Write-Host "  * Real tests: Use -SetupCredentials flag to configure"
Write-Host "  * Coverage: Check TestResults/Coverage/index.html"
Write-Host ""
