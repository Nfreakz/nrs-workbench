param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Pull', 'Push')]
    [string]$Direction
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$wikiDir = Join-Path $repoRoot 'wiki'
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("nrs-workbench-wiki-" + [Guid]::NewGuid().ToString('N'))
$wikiRemote = 'https://github.com/Nfreakz/nrs-workbench.wiki.git'

function Require-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'Git is required but was not found in PATH.'
    }
}

function Copy-Tree {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    Get-ChildItem -LiteralPath $Destination -Force -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force

    Get-ChildItem -LiteralPath $Source -Force |
        Where-Object { $_.Name -ne '.git' } |
        ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
        }
}

Require-Git

try {
    git clone --quiet $wikiRemote $tempDir
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to clone the GitHub Wiki repository: $wikiRemote"
    }

    if ($Direction -eq 'Pull') {
        Copy-Tree -Source $tempDir -Destination $wikiDir
        Write-Host "Wiki imported into: $wikiDir"
        Write-Host 'Review the files, then commit them through the normal pull-request flow.'
        git -C $repoRoot status --short -- wiki
        exit 0
    }

    if (-not (Test-Path -LiteralPath $wikiDir -PathType Container)) {
        throw "Wiki source folder does not exist: $wikiDir. Run with -Direction Pull first."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $wikiDir 'Home.md') -PathType Leaf)) {
        throw 'wiki/Home.md is required before publishing.'
    }

    Copy-Tree -Source $wikiDir -Destination $tempDir

    git -C $tempDir add -A
    git -C $tempDir diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host 'GitHub Wiki is already synchronized.'
        exit 0
    }

    git -C $tempDir config user.name 'Neo RS Docs'
    git -C $tempDir config user.email 'nrs-workbench-docs@example.invalid'
    git -C $tempDir commit -m 'docs: sync NRS Workbench wiki'
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to create the Wiki synchronization commit.'
    }

    git -C $tempDir push origin HEAD
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to push the Wiki. Check your GitHub authentication and Wiki permissions.'
    }

    Write-Host 'GitHub Wiki synchronized successfully.'
}
finally {
    if (Test-Path -LiteralPath $tempDir) {
        Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
