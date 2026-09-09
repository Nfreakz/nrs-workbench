$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Write-Host '1/3 Building Release...' -ForegroundColor Cyan
    dotnet build .\NRSWorkbench.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE" }

    Write-Host '2/3 Running Git safety smoke tests...' -ForegroundColor Cyan
    dotnet run --project .\tests\NRS.Workbench.SmokeTests\NRS.Workbench.SmokeTests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Smoke tests failed with exit code $LASTEXITCODE" }

    Write-Host '3/3 Running public source audit...' -ForegroundColor Cyan
    & .\scripts\public-audit.ps1
    if ($LASTEXITCODE -ne 0) { throw "Public audit failed with exit code $LASTEXITCODE" }

    Write-Host ''
    Write-Host 'PUBLIC READINESS PASSED' -ForegroundColor Green
} finally {
    Pop-Location
}
