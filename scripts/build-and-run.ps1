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

function Update-RepositorySafely {
    $gitMetadata = Join-Path $root '.git'
    if (-not (Test-Path -LiteralPath $gitMetadata)) {
        Write-Host 'No es un checkout Git; se usaran los archivos actuales sin intentar actualizar.' -ForegroundColor Yellow
        return
    }

    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) {
        throw 'Este checkout contiene .git pero Git no esta disponible en PATH. Instala Git for Windows para actualizar antes de compilar.'
    }

    $insideWorkTree = [string](& git -C $root rev-parse --is-inside-work-tree 2>$null)
    $insideWorkTree = $insideWorkTree.Trim()
    if ($LASTEXITCODE -ne 0 -or $insideWorkTree -ne 'true') {
        throw 'La carpeta contiene metadatos Git pero no se puede validar como working tree.'
    }

    $branch = [string](& git -C $root symbolic-ref --quiet --short HEAD 2>$null)
    $branch = $branch.Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branch)) {
        throw 'El repositorio esta en detached HEAD. No se actualizara automaticamente para evitar cambiar de revision sin permiso.'
    }

    $changes = @(& git -C $root status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) {
        throw 'No se ha podido comprobar el estado local del repositorio.'
    }
    if ($changes.Count -gt 0) {
        throw "Hay cambios locales en '$branch'. RUN_ME_FIRST no hara pull ni descartara archivos. Guarda, haz commit o stash y vuelve a ejecutarlo."
    }

    $upstream = [string](& git -C $root rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' 2>$null)
    $upstream = $upstream.Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($upstream)) {
        throw "La rama '$branch' no tiene upstream configurado. No puedo garantizar que sea la ultima version sin elegir un remoto por ti."
    }

    if ($branch -ne 'main') {
        Write-Host "Nota: estas en '$branch'. Se actualizara contra '$upstream'; RUN_ME_FIRST no cambiara automaticamente a main." -ForegroundColor Yellow
    }

    $before = [string](& git -C $root rev-parse --short HEAD)
    $before = $before.Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'No se ha podido leer la revision Git actual.'
    }

    Write-Host "Actualizando repositorio: $branch <- $upstream"
    & git -C $root fetch --prune
    if ($LASTEXITCODE -ne 0) {
        throw "git fetch ha fallado con codigo $LASTEXITCODE. No se compilara una copia potencialmente desactualizada."
    }

    & git -C $root pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        throw "git pull --ff-only ha fallado con codigo $LASTEXITCODE. No se han hecho merges, rebase ni reset automaticos."
    }

    $after = [string](& git -C $root rev-parse --short HEAD)
    $after = $after.Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'El repositorio se ha actualizado pero no se ha podido leer la revision final.'
    }

    if ($before -eq $after) {
        Write-Host "Repositorio al dia ($after)." -ForegroundColor Green
    }
    else {
        Write-Host "Repositorio actualizado: $before -> $after" -ForegroundColor Green
    }
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

    Write-Host 'Comprobando actualizaciones del repositorio...'
    Update-RepositorySafely
    Write-Host ''

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
