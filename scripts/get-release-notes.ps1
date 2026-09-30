param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version,

    [string]$ChangelogPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'CHANGELOG.md')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ChangelogPath -PathType Leaf)) {
    throw "Changelog not found: $ChangelogPath"
}

$changelog = Get-Content -LiteralPath $ChangelogPath -Raw
$escapedVersion = [regex]::Escape($Version)
$pattern = '(?ms)^## \[' + $escapedVersion + '\][^\r\n]*\r?\n(?<body>.*?)(?=^## \[|\z)'
$match = [regex]::Match($changelog, $pattern)

if (-not $match.Success) {
    throw "CHANGELOG.md does not contain release notes for v$Version."
}

$body = $match.Groups['body'].Value.Trim()
if ([string]::IsNullOrWhiteSpace($body)) {
    throw "Release notes for v$Version are empty."
}

$body
