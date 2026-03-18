# scripts/test-loop.ps1
# Self-healing test runner — build once, run up to MaxIterations times until green.
# Usage:
#   .\scripts\test-loop.ps1                        # run all tests
#   .\scripts\test-loop.ps1 -Milestone M1          # run only M1 tests
#   .\scripts\test-loop.ps1 -Milestone "M1|M2"     # run M1 and M2 tests

param(
    [string]$Milestone = "",
    [int]$MaxIterations = 5,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path $PSScriptRoot -Parent

function Run-Tests {
    param([string]$Filter)
    $args = @(
        "test", "$RootDir\SpotDesk.sln",
        "--no-build",
        "--configuration", $Configuration,
        "--logger", "console;verbosity=detailed",
        "--logger", "trx;LogFileName=results.trx",
        "--results-directory", "$RootDir\TestResults"
    )
    if ($Filter) {
        $args += @("--filter", "Category=$Filter")
    }
    & dotnet @args
    return $LASTEXITCODE
}

# ── Build once ────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Building solution ($Configuration) ===" -ForegroundColor Cyan
& dotnet build "$RootDir\SpotDesk.sln" --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "BUILD FAILED — fix compile errors before running tests" -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded." -ForegroundColor Green

# ── Test loop ─────────────────────────────────────────────────────────────────
$Iteration = 0
$Pass = $false

while ($Iteration -lt $MaxIterations) {
    $Iteration++
    Write-Host ""
    Write-Host "=== Test run $Iteration / $MaxIterations ===" -ForegroundColor Cyan

    $exitCode = Run-Tests -Filter $Milestone
    if ($exitCode -eq 0) {
        $Pass = $true
        break
    }

    Write-Host ""
    Write-Host "=== Failures detected on iteration $Iteration ===" -ForegroundColor Yellow
}

# ── Result ────────────────────────────────────────────────────────────────────
Write-Host ""
if ($Pass) {
    Write-Host "✓ ALL TESTS PASSED on iteration $Iteration" -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ Still failing after $MaxIterations iterations — diagnostic report needed" -ForegroundColor Red
    exit 1
}
