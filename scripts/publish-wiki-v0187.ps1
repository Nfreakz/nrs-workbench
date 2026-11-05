$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:GITHUB_REPOSITORY -cne 'Nfreakz/nrs-workbench') {
    throw 'This one-time Wiki publisher only supports Nfreakz/nrs-workbench.'
}
if ($env:GITHUB_REF -cne 'refs/heads/main') {
    throw 'The Wiki publisher must run from the protected main branch.'
}
if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    throw 'GitHub workflow token unavailable.'
}

$root = Split-Path -Parent $PSScriptRoot
$guidePath = Join-Path $root 'docs\PORTABLE_WORKSPACE.md'
if (-not (Test-Path -LiteralPath $guidePath -PathType Leaf)) {
    throw 'Portable Workspace guide missing.'
}
$wikiDir = Join-Path $env:RUNNER_TEMP "nrs-workbench-wiki-$env:GITHUB_RUN_ID"
if (Test-Path -LiteralPath $wikiDir) { throw 'Refusing to overwrite an existing Wiki workspace.' }

# Use only the short-lived workflow token. Never place credentials in remote URLs.
$basic = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("x-access-token:$env:GITHUB_TOKEN"))
Write-Host "::add-mask::$basic"
$authHeader = "AUTHORIZATION: basic $basic"
$wikiUrl = "https://github.com/$($env:GITHUB_REPOSITORY).wiki.git"
& git -c "http.https://github.com/.extraheader=$authHeader" clone --depth 1 $wikiUrl $wikiDir
if ($LASTEXITCODE -ne 0) { throw 'Failed to clone the existing GitHub Wiki.' }

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
}
function Write-VersionedPage([string]$Name, [string]$Content) {
    $file = Join-Path $wikiDir $Name
    if (Test-Path -LiteralPath $file -PathType Leaf) {
        $current = Get-Content -LiteralPath $file -Raw -Encoding UTF8
        if ($current -cne $Content) {
            throw "Existing Wiki page $Name differs. Refusing to overwrite it."
        }
        return
    }
    Write-Utf8NoBom -Path $file -Content $Content
}

$nl = [Environment]::NewLine
$homePath = Join-Path $wikiDir 'Home.md'
if (-not (Test-Path -LiteralPath $homePath -PathType Leaf)) {
    throw 'Wiki Home.md does not exist; will not initialize or replace the Wiki.'
}
$home = Get-Content -LiteralPath $homePath -Raw -Encoding UTF8
$startMarker = '<!-- neors-v0187-release-start -->'
$endMarker = '<!-- neors-v0187-release-end -->'
$latest = @'
<!-- neors-v0187-release-start -->
## Latest Public Preview: NRS Workbench v0.18.7

**[Release notes](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.7) | [Windows x64 ZIP](https://github.com/Nfreakz/nrs-workbench/releases/download/v0.18.7/NRSWorkbench-v0.18.7-win-x64.zip)**

v0.18.7 adds Portable Workspace: an adjacent Data directory, removable-drive-relative paths and optional preparation of stopped interactive GitHub.com runners. It also includes the v0.18.6 Maintenance Center, Runner Doctor, Smart Queue and Feedback & Diagnostics.

**Public Preview safety:** runner re-registration across two physical PCs has not been independently validated. Use only disposable stopped interactive runners for initial tests. Service-installed runners and automatic migration on drive insertion are not supported.

Documentation: [Portable Workspace v0.18.7](Portable-Workspace-v0.18.7) | [Maintenance Center v0.18.6](Maintenance-Center-v0.18.6) | [Repository](https://github.com/Nfreakz/nrs-workbench).
<!-- neors-v0187-release-end -->
'@
if ($home.Contains($startMarker)) {
    $start = $home.IndexOf($startMarker, [StringComparison]::Ordinal)
    $end = $home.IndexOf($endMarker, $start, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw 'Wiki Home release markers are not balanced.' }
    $home = $home.Substring(0, $start) + $latest + $home.Substring($end + $endMarker.Length)
}
elseif ($home -match '\A(#[^\r\n]+\r?\n)') {
    $titleLine = $Matches[1]
    $home = $titleLine + $nl + $latest + $nl + $nl + $home.Substring($titleLine.Length)
}
else {
    $home = $latest + $nl + $nl + $home
}
Write-Utf8NoBom -Path $homePath -Content $home

$guide = Get-Content -LiteralPath $guidePath -Raw -Encoding UTF8
$portablePage = '# Portable Workspace v0.18.7' + $nl + $nl +
    '[Source guide in the NRS Workbench repository](https://github.com/Nfreakz/nrs-workbench/blob/main/docs/PORTABLE_WORKSPACE.md)' + $nl + $nl + $guide
Write-VersionedPage -Name 'Portable-Workspace-v0.18.7.md' -Content $portablePage

$maintenancePage = @'
# Maintenance Center v0.18.6

Included in [NRS Workbench v0.18.6](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.6) and later.

- Workbench and PREVIEW logs can be reviewed for low-risk cleanup.
- Old runner diagnostic files in _diag are opt-in; removing them reduces local history.
- The newest Runner and Worker logs survive cleanup. BUSY runners are skipped.
- Runner _work is inventory-only; automatic cleanup never deletes it.
- Long scans stream partial results and label incomplete measurements.
- Unsafe paths, Windows junctions and reparse points are excluded from cleanup.

See [README](https://github.com/Nfreakz/nrs-workbench) and [Testing](https://github.com/Nfreakz/nrs-workbench/blob/main/docs/TESTING.md). First test cleanup on disposable runners and non-production logs.
'@
Write-VersionedPage -Name 'Maintenance-Center-v0.18.6.md' -Content $maintenancePage

$sidebarPath = Join-Path $wikiDir '_Sidebar.md'
$sidebarMarker = '<!-- neors-v0187-pages -->'
if (Test-Path -LiteralPath $sidebarPath -PathType Leaf) {
    $sidebar = Get-Content -LiteralPath $sidebarPath -Raw -Encoding UTF8
    if (-not $sidebar.Contains($sidebarMarker)) {
        $sidebar += $nl + $nl + $sidebarMarker + $nl +
            '* [Portable Workspace v0.18.7](Portable-Workspace-v0.18.7)' + $nl +
            '* [Maintenance Center v0.18.6](Maintenance-Center-v0.18.6)' + $nl
        Write-Utf8NoBom -Path $sidebarPath -Content $sidebar
    }
}

Push-Location $wikiDir
try {
    git config user.name 'NRS Workbench Documentation'
    git config user.email 'nrs-workbench-ci@example.invalid'
    $files = @('Home.md', 'Portable-Workspace-v0.18.7.md', 'Maintenance-Center-v0.18.6.md')
    if (Test-Path -LiteralPath $sidebarPath -PathType Leaf) { $files += '_Sidebar.md' }
    & git add -- @files
    if ($LASTEXITCODE -ne 0) { throw 'Failed to stage Wiki pages.' }
    $changes = @(git diff --cached --name-only)
    if ($LASTEXITCODE -ne 0) { throw 'Could not read staged Wiki changes.' }
    if ($changes.Count -eq 0) { Write-Host 'Wiki already up to date.'; exit 0 }
    Write-Host ('Updating Wiki pages: ' + ($changes -join ', '))
    & git commit -m 'docs: NRS Workbench Wiki v0.18.7 Public Preview'
    if ($LASTEXITCODE -ne 0) { throw 'Wiki commit failed.' }
    $localSha = (& git rev-parse HEAD).Trim()
    & git -c "http.https://github.com/.extraheader=$authHeader" push origin HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Wiki push failed; no force push attempted.' }
    $remote = (& git -c "http.https://github.com/.extraheader=$authHeader" ls-remote origin HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $remote.StartsWith($localSha, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Wiki push returned successfully but remote HEAD did not match.'
    }
    Write-Host "Wiki remote update verified: $localSha"
}
finally {
    Pop-Location
}
