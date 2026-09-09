# Public Readiness Audit — v0.15.1

Date: 2026-09-09

This audit carries forward the fully passing v0.14.3 public-readiness baseline and verifies the v0.15.1 NRS Workbench rebrand. The Git write behavior is unchanged. The Windows readiness gate must still pass build, 22 Git safety smoke tests and public source audit before tagging the first public preview.

## Results

- Git behavior scenarios: **18/18 passed** in isolated temporary repositories.
- XAML structural parsing: **8/8 passed**.
- GitHub workflow / issue-template YAML parsing: **5/5 passed**.
- Common secret-pattern scan: **passed** after removing synthetic token literals from test source.
- Hard-coded personal path/account scan in `src/`: **passed**.
- Generated/sensitive-file presence audit: **passed**.
- Runtime .NET compilation in this audit environment: **not available** (`dotnet` not installed).

## Git behaviors exercised

The isolated Git tests covered:

1. Partial commit leaves an unselected file untouched.
2. Partial commit contains only the selected path.
3. Empty commit message guard occurs before index mutation.
4. Pre-staged files outside the selection are detected and blocked by the service logic.
5. Porcelain parsing handles spaces, leading dashes and Unicode filenames.
6. `git add -A -- <path>` safely handles those filenames.
7. Rename parsing preserves original/new path ordering.
8. A fully staged rename commits without re-adding an obsolete original pathspec.
9. A staged rename with additional working-tree edits stages and commits the current destination path safely.
10. Selected deletion commits correctly.
11. Ahead state is observable before push.
12. Push returns the local branch to zero ahead commits.
13. Behind state is observable after fetch.
14. Clean behind branch can fast-forward with `pull --ff-only`.
15. Divergence is observable as ahead + behind.
16. `pull --ff-only` refuses divergent history.
17. Git refuses non-fast-forward push.
18. Merge conflicts produce porcelain-v2 unmerged records recognized by the current conflict model.

## Hardening added during the audit

### Remote credential display

A repository can technically contain an HTTP(S) remote with embedded user-info. Displaying raw `git remote get-url origin` output could therefore expose a username/password/token in the UI. v0.14 sanitizes remote URLs before display and reconstructs GitHub browser links without user-info.

### Diagnostic redaction

Application log text and Git command error messages now pass through a best-effort redactor for GitHub token-shaped strings and HTTP(S) user-info.

### Diff helpers

Read-only diff preview now uses both `--no-ext-diff` and `--no-textconv`, reducing the chance of repository/user Git configuration invoking external diff/text-conversion helpers during a preview.

### Reparse points

Small untracked-file previews refuse reparse points, and folder repository scanning skips reparse-point directories. This avoids silently following junction/symlink-style filesystem indirection during convenience reads/scans.

## Public repository gates

Before a tag is published, run on Windows:

```powershell
dotnet build .\NRSWorkbench.sln -c Release
dotnet run --project .\tests\NRS.Workbench.SmokeTests\NRS.Workbench.SmokeTests.csproj -c Release
.\scripts\public-audit.ps1
```

Or run `RUN_PUBLIC_READINESS.cmd`.


## Licensing gate

Before the first public release, the source audit verifies that `LICENSE` identifies `PolyForm-Noncommercial-1.0.0`, that the Required Notice for Neo RS is present, and that the current README/About UI identify the project as source-available rather than MIT/open-source licensed. External code pull requests remain disabled until a contributor agreement suitable for separate commercial licensing is published.
