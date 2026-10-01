$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repoParent = Split-Path -Parent $root
$repoName = Split-Path -Leaf $root
$previewRoot = Join-Path $repoParent ($repoName + '-preview')
$previewRef = 'origin/qa-preview'
$normalData = Join-Path $env:LOCALAPPDATA 'NRSWorkbench'
$previewData = Join-Path $env:LOCALAPPDATA 'NRSWorkbenchPreview'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Stop-WorkbenchInstances {
    $matches = @(Get-Process -Name 'NRSWorkbench' -ErrorAction SilentlyContinue)
    foreach ($process in $matches) {
        Write-Host "Cerrando NRS Workbench PID $($process.Id)..."
        try { [void]$process.CloseMainWindow() } catch { }
        try { [void]$process.WaitForExit(1500) } catch { }
        $process.Refresh()
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            try { $process.WaitForExit(1500) } catch { }
        }
    }
}

function Test-RegisteredPreviewWorktree {
    $paths = @()
    foreach ($line in @(& git -C $root worktree list --porcelain)) {
        if ($line -like 'worktree *') {
            $paths += $line.Substring(9).Trim()
        }
    }

    return @($paths | Where-Object {
        try { [IO.Path]::GetFullPath($_) -eq [IO.Path]::GetFullPath($previewRoot) }
        catch { $false }
    }).Count -gt 0
}

if (-not (Test-IsAdministrator)) {
    $quotedScript = '"{0}"' -f $PSCommandPath
    Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $quotedScript) -WorkingDirectory $root -Verb RunAs
    exit 0
}

Clear-Host
Write-Host '=========================================================='
Write-Host '  NRS Workbench - PREVIEW'
Write-Host '=========================================================='
Write-Host ''
Write-Host 'La copia normal no cambiara de rama.' -ForegroundColor Green
Write-Host "Preview local: $previewRoot"
Write-Host 'Canal remoto: qa-preview'
Write-Host ''

try {
    if (-not (Test-Path -LiteralPath (Join-Path $root '.git'))) {
        throw 'RUN_PREVIEW necesita ejecutarse desde el checkout Git normal de NRS Workbench.'
    }

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'Git for Windows no esta disponible en PATH.'
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'No se encuentra el SDK de .NET. Instala .NET 8 SDK o posterior.'
    }

    Stop-WorkbenchInstances

    Write-Host 'Actualizando canal de pruebas...'
    & git -C $root fetch origin +refs/heads/qa-preview:refs/remotes/origin/qa-preview --prune
    if ($LASTEXITCODE -ne 0) {
        throw 'No se ha podido descargar origin/qa-preview.'
    }

    & git -C $root rev-parse --verify --quiet $previewRef *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'El canal qa-preview no existe todavia en el remoto.'
    }

    if (-not (Test-RegisteredPreviewWorktree)) {
        if (Test-Path -LiteralPath $previewRoot) {
            throw "La carpeta de preview ya existe pero no pertenece a esta worktree: $previewRoot"
        }

        Write-Host 'Creando worktree de pruebas...'
        & git -C $root worktree add --detach $previewRoot $previewRef
        if ($LASTEXITCODE -ne 0) {
            throw 'No se ha podido crear la worktree de preview.'
        }
    }
    else {
        Write-Host 'Sincronizando worktree de pruebas...'
        & git -C $previewRoot reset --hard $previewRef
        if ($LASTEXITCODE -ne 0) {
            throw 'No se ha podido actualizar la worktree de preview.'
        }

        & git -C $previewRoot clean -fdx
        if ($LASTEXITCODE -ne 0) {
            throw 'No se ha podido limpiar la worktree de preview.'
        }
    }

    $previewSha = (& git -C $previewRoot rev-parse --short HEAD).Trim()
    Write-Host "Preview preparada en $previewSha" -ForegroundColor Green

    Write-Host 'Preparando perfil local aislado...'
    New-Item -ItemType Directory -Force -Path $previewData | Out-Null
    $normalSettings = Join-Path $normalData 'settings.json'
    $previewSettings = Join-Path $previewData 'settings.json'

    if (Test-Path -LiteralPath $normalSettings -PathType Leaf) {
        Copy-Item -LiteralPath $normalSettings -Destination $previewSettings -Force
    }
    elseif (Test-Path -LiteralPath $previewSettings -PathType Leaf) {
        Remove-Item -LiteralPath $previewSettings -Force
    }

    $previewQueueState = Join-Path $previewData 'runner-queue-state.json'
    if (Test-Path -LiteralPath $previewQueueState -PathType Leaf) {
        Remove-Item -LiteralPath $previewQueueState -Force
    }

    $sln = Join-Path $previewRoot 'NRSWorkbench.sln'
    $exe = Join-Path $previewRoot 'src\NRS.Workbench.App\bin\Release\net8.0-windows\NRSWorkbench.exe'
    if (-not (Test-Path -LiteralPath $sln -PathType Leaf)) {
        throw "La preview no contiene la solucion esperada: $sln"
    }

    Write-Host ''
    Write-Host 'Compilando preview...'
    & dotnet build $sln -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "La compilacion preview ha fallado con codigo $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        throw "No encuentro el ejecutable preview: $exe"
    }

    Write-Host ''
    Write-Host 'Abriendo NRS Workbench · PREVIEW' -ForegroundColor Yellow

    $previousProfile = $env:NRS_WORKBENCH_PROFILE
    try {
        $env:NRS_WORKBENCH_PROFILE = 'preview'
        Start-Process -FilePath $exe -WorkingDirectory $previewRoot
    }
    finally {
        if ($null -eq $previousProfile) {
            Remove-Item Env:NRS_WORKBENCH_PROFILE -ErrorAction SilentlyContinue
        }
        else {
            $env:NRS_WORKBENCH_PROFILE = $previousProfile
        }
    }
}
catch {
    Write-Host ''
    Write-Host '[ERROR]' -ForegroundColor Red -NoNewline
    Write-Host " $($_.Exception.Message)"
    Write-Host ''
    Read-Host 'Presiona ENTER para cerrar'
    exit 1
}
