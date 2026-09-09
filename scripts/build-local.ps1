$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build .\NRSWorkbench.sln -c Release
} finally { Pop-Location }
