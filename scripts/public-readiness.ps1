$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))

function Stop-CheckoutBuildInstances {
    $outputRoots = @(
        (Join-Path $root 'src\NRS.Workbench.App\bin\Release'),
        (Join-Path $root 'src\RunnerManager.App\bin\Release')
    ) | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\') + '\' }

    $processNames = @('NRSWorkbench', 'GitHubWorkspaceManager', 'RunnerManager')
    $closed = 0

    foreach ($processName in $processNames) {
        $processes = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
        foreach ($process in $processes) {
            try {
                $processPath = [IO.Path]::GetFullPath($process.Path)
            }
            catch {
                continue
            }

            $belongsToCheckout = $false
            foreach ($outputRoot in $outputRoots) {
                if ($processPath.StartsWith($outputRoot, [StringComparison]::OrdinalIgnoreCase)) {
                    $belongsToCheckout = $true
                    break
                }
            }

            if (-not $belongsToCheckout) {
                continue
            }

            Write-Host "Closing local test instance $([IO.Path]::GetFileName($processPath)) (PID $($process.Id)) before build..." -ForegroundColor Yellow
            try { [void]$process.CloseMainWindow() } catch { }
            try { [void]$process.WaitForExit(1500) } catch { }

            try { $process.Refresh() } catch { }
            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force -ErrorAction Stop
                try { [void]$process.WaitForExit(1500) } catch { }
            }

            $closed++
        }
    }

    if ($closed -eq 0) {
        Write-Host 'No local build instance is locking this checkout.' -ForegroundColor DarkGray
    }
}

Push-Location $root
try {
    Stop-CheckoutBuildInstances

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
