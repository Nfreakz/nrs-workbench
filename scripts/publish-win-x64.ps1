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
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    $exe = Join-Path $out 'NRSWorkbench.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Portable publish did not create $exe" }
    if ((Get-Item -LiteralPath $exe).Length -lt 1MB) { throw "Portable executable is unexpectedly small: $exe" }
    Write-Host "Publicado en: $out" -ForegroundColor Green
} finally { Pop-Location }
