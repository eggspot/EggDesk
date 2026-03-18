# scripts/publish.ps1 — Build a single-file SpotDesk executable
# Usage:
#   .\scripts\publish.ps1                  # defaults to win-x64
#   .\scripts\publish.ps1 -Rid osx-arm64

param(
    [string]$Rid = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RootDir = Split-Path $PSScriptRoot -Parent
$Project = "$RootDir\src\SpotDesk.App\SpotDesk.App.csproj"
$OutputDir = "$RootDir\artifacts\$Rid"

Write-Host ""
Write-Host "=== Publishing SpotDesk ===" -ForegroundColor Cyan
Write-Host "  RID:    $Rid"
Write-Host "  Config: $Configuration"
Write-Host "  Output: $OutputDir"
Write-Host ""

dotnet publish $Project `
    -r $Rid `
    -c $Configuration `
    -o $OutputDir `
    --self-contained true

Write-Host ""
Write-Host "=== Build complete ===" -ForegroundColor Green

$exe = Join-Path $OutputDir "SpotDesk.exe"
if (Test-Path $exe) {
    $size = (Get-Item $exe).Length / 1MB
    Write-Host "Output: $exe ($([math]::Round($size, 1)) MB)"
}

Write-Host ""
Write-Host "Run with:  $OutputDir\SpotDesk.exe"
