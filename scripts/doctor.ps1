$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root 'NRSWorkbench.sln'
$app = Join-Path $root 'src\NRS.Workbench.App\NRS.Workbench.App.csproj'

Write-Host 'NRS Workbench - Doctor' -ForegroundColor Cyan
Write-Host "Root: $root"
Write-Host "Solution exists: $(Test-Path $sln)"
Write-Host "App project exists: $(Test-Path $app)"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    Write-Host "dotnet: $($dotnet.Source)"
    & dotnet --version
} else {
    Write-Warning '.NET SDK no encontrado.'
}
