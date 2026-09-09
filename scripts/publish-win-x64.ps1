$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'dist\win-x64'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
Push-Location $root
try {
    dotnet publish .\src\NRS.Workbench.App\NRS.Workbench.App.csproj `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishReadyToRun=true `
        -o $out
    Write-Host "Publicado en: $out" -ForegroundColor Green
} finally { Pop-Location }
