$ErrorActionPreference = 'Stop'
$files = @(Get-ChildItem .github/workflows -File | Where-Object { $_.Extension -in '.yml', '.yaml' })
if ($files.Count -eq 0) { throw 'No workflow files found' }
foreach ($file in $files) {
  $blockIndent = -1
  foreach ($line in Get-Content $file.FullName) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $indent = $line.Length - $line.TrimStart().Length
    if ($blockIndent -ge 0 -and $indent -gt $blockIndent) { continue }
    $blockIndent = -1
    $structural = ($line -replace '\s+#.*$', '').Trim()
    if ($structural.StartsWith('#')) { continue }
    if ($structural -match '^[\w-]+:\s*[|>][-+]?\s*$') { $blockIndent = $indent; continue }
    if ($structural -match '(?i)\b(?:ubuntu|windows|macos)-(?:latest|\d[\w.-]*)\b') {
      throw "GitHub-hosted runner label in $($file.Name): $($Matches[0])"
    }
  }
}
Write-Host 'Runner policy passed: no GitHub-hosted OS labels'
