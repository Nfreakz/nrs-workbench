$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sln = Join-Path $root 'NRSWorkbench.sln'
$appProject = Join-Path $root 'src\NRS.Workbench.App\NRS.Workbench.App.csproj'
$exe = Join-Path $root 'src\NRS.Workbench.App\bin\Release\net8.0-windows\NRSWorkbench.exe'
$legacyWorkspaceExe = Join-Path $root 'src\RunnerManager.App\bin\Release\net8.0-windows\GitHubWorkspaceManager.exe'
$legacyRunnerExe = Join-Path $root 'src\RunnerManager.App\bin\Release\net8.0-windows\RunnerManager.exe' 

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    $quotedScript = '"{0}"' -f $PSCommandPath
    Start-Process -FilePath 'powershell.exe' `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $quotedScript) `
        -WorkingDirectory $root `
        -Verb RunAs
    exit 0
}

Clear-Host
Write-Host '=========================================================='
Write-Host '  NRS Workbench - Build & Run'
Write-Host '=========================================================='
Write-Host ''
Write-Host 'Carpeta detectada:'
Write-Host "  $root"
Write-Host ''

try {
    if (-not (Test-Path -LiteralPath $sln -PathType Leaf)) {
        throw "No encuentro NRSWorkbench.sln en: $sln`nExtrae TODO el ZIP a una carpeta normal antes de ejecutar RUN_ME_FIRST.cmd."
    }

    if (-not (Test-Path -LiteralPath $appProject -PathType Leaf)) {
        throw "La estructura del proyecto esta incompleta. Falta: $appProject"
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw 'No se encuentra el SDK de .NET. Instala .NET 8 SDK o posterior.'
    }

    $dotnetVersion = (& dotnet --version).Trim()
    Write-Host ".NET SDK: $dotnetVersion"
    Write-Host "Solucion: $sln"
    Write-Host ''

    Write-Host 'Comprobando si hay una instancia anterior abierta...'
    $candidateExecutables = @($exe, $legacyWorkspaceExe, $legacyRunnerExe) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    foreach ($candidate in $candidateExecutables) {
        $target = [IO.Path]::GetFullPath($candidate)
        $processName = [IO.Path]::GetFileNameWithoutExtension($candidate)
        $matches = @(
            Get-Process -Name $processName -ErrorAction SilentlyContinue |
                Where-Object {
                    try { [IO.Path]::GetFullPath($_.Path) -eq $target }
                    catch { $false }
                }
        )

        foreach ($process in $matches) {
            Write-Host "Cerrando $([IO.Path]::GetFileName($candidate)) PID $($process.Id)..."
            try { [void]$process.CloseMainWindow() } catch { }
            try { [void]$process.WaitForExit(1200) } catch { }
            $process.Refresh()
            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force -ErrorAction Stop
                try { $process.WaitForExit(1500) } catch { }
            }
        }
    }

    Write-Host ''
    Write-Host 'Compilando NRS Workbench...'
    Write-Host ''

    & dotnet build $sln -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "La compilacion ha fallado con codigo $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        throw "La compilacion termino pero no encuentro el ejecutable esperado: $exe"
    }

    Write-Host ''
    Write-Host 'Compilacion correcta.' -ForegroundColor Green
    Write-Host 'Iniciando NRS Workbench como administrador...'
    Write-Host ''
    Start-Process -FilePath $exe -WorkingDirectory $root
}
catch {
    Write-Host ''
    Write-Host '[ERROR]' -ForegroundColor Red -NoNewline
    Write-Host " $($_.Exception.Message)"
    Write-Host ''
    Write-Host 'Si es un error de compilacion, copia desde la primera linea de error hasta el final.'
    Write-Host ''
    Read-Host 'Presiona ENTER para cerrar'
    exit 1
}
