# Changelog

## [0.15.1] - 2026-09-09

### Licensing
- Replaces the pre-public MIT licensing plan with **PolyForm Noncommercial License 1.0.0** (`PolyForm-Noncommercial-1.0.0`).
- Marks NRS Workbench as **source-available** rather than OSI open source because the public license restricts commercial use.
- Adds the required Neo RS copyright notice and a separate `COMMERCIAL-LICENSING.md` policy for commercial-use enquiries.
- Updates the About window and README so the public licensing model is visible before download/use.
- Temporarily disables external code pull requests until a contributor agreement is published that preserves Neo RS's ability to grant separate commercial licenses for community-contributed code.
- Adds public-audit checks that fail if the expected PolyForm SPDX identifier/required notice are missing or current-facing README/About text drifts back to MIT/open-source wording.

### Safety
- No runner-control or Git write behavior changed from v0.15.0; the existing 22 Git safety smoke tests remain the release gate.

## [0.15.0] - 2026-09-09

### Product identity
- Renames the public product to **NRS Workbench** and the executable to `NRSWorkbench.exe`.
- Renames public solution, project and .NET namespaces to `NRS.Workbench.*`.
- Adds the first NRS Workbench application icon and uses it for the executable, WPF windows, main/About branding and Windows notification-area icon.
- Adds public assembly company/copyright metadata for Neo RS.

### Migration
- Moves new local settings and application logs to `%LOCALAPPDATA%\NRSWorkbench`.
- On first launch, copies legacy `%LOCALAPPDATA%\RunnerManager\settings.json` when no NRS Workbench settings exist, preserving runner roots, repository paths and preferences without deleting the legacy file.

### Safety
- No Git write behavior changed from the audited v0.14.3 baseline. The same guarded Fetch/Pull/Push/Commit rules and 22 Git safety smoke-test scenarios remain in place.

## [0.14.3] - 2026-09-09

### Fixed
- Fixes the public-readiness source audit so generated build folders (`bin`, `obj`, `dist`, `artifacts`, `TestResults`) are excluded consistently on both Windows and Unix-style paths.
- The previous source-only filter matched `/bin/` and `/obj/` but not Windows `\bin\` / `\obj\`, so a successful readiness build caused its own generated DLL/NuGet/MSBuild files to be mistaken for hard-coded personal paths.
- Centralizes audit exclusions in one helper and also ignores VCS/IDE metadata (`.git`, `.vs`, `.idea`) while still failing if generated artifacts are actually tracked by Git.

## [0.14.2] - 2026-09-09

### Fixed
- Fixes a false positive in the public source audit where regex literals such as `Starting:\s*` were mistaken for a personal `G:\...` Windows path because PowerShell `-match` is case-insensitive by default.
- Narrows the personal-drive check to standalone drive-path tokens and adds audit regression guards that verify regex snippets are accepted while real `G:\Private\...` paths are still rejected.

## [0.14.1] - 2026-09-09

### Fixed
- Fixes commit staging for already-staged renames created with `git mv`; the commit flow no longer passes an obsolete original path back to `git add`.
- Keeps partial-commit safety intact: unstaged renames still stage both old and new paths, while staged files with additional working-tree edits stage only their current path.
- Adds regression smoke tests for both fully staged renames and staged renames that also have additional working-tree edits.

## [0.14.0] - 2026-09-09

### Public preview readiness

- Adds a dependency-free `RunnerManager.SmokeTests` executable that exercises the real Git service against temporary local repositories/remotes.
- Adds Windows CI and tag-driven portable release workflows.
- Adds `public-audit.ps1`, `.editorconfig`, Code of Conduct, issue/PR templates and architecture/privacy/testing/release documentation.
- Sanitizes repository remote URLs before display so embedded HTTP(S)/SSH credentials are not exposed.
- Redacts embedded HTTP(S) user-info and GitHub token-shaped secrets from application diagnostics and Git command error messages.
- Reconstructs GitHub browser URLs without remote user-info.
- Disables external diff and textconv helpers in diff preview.
- Skips reparse points for untracked-file preview and repository-folder scanning.
- Forces UTF-8 decoding for Git stdout/stderr to make porcelain/path parsing more predictable with non-ASCII filenames.
- Renames the public executable to `GitHubWorkspaceManager.exe` while preserving internal namespaces and `%LOCALAPPDATA%\RunnerManager` migration compatibility.
- Adds public assembly metadata for product, author and description.

## [0.13.0] - 2026-09-09

### Added
- Adds a guarded repository **Commit...** workflow with per-file selection.
- Adds a textual diff/preview panel for tracked changes plus safe previews for small untracked text files.
- Adds **Commit & Push** for branches whose upstream is known and not ahead.
- Adds file-level Git status metadata (`staged`, working-tree changes, untracked, rename, conflict) to the Core domain.
- Adds a dedicated commit window that shows selected files, status codes, branch, message and a final confirmation summary.

### Safety
- Commits are blocked when unresolved conflicts exist.
- Existing staged files outside the user's selected set block the commit instead of being included implicitly.
- Selected paths are staged with `git add -A -- <paths>` immediately before commit so deletions and untracked files are handled consistently.
- `Commit & Push` never performs pull/rebase/merge implicitly; push remains blocked if the remote is ahead or no upstream is configured.
- Diff preview is read-only and capped in size to keep the desktop UI responsive.

## [0.12.0] - 2026-09-09

### Product evolution
- The visible product name becomes **GitHub Workspace Manager**. Internal assembly/namespace and `%LOCALAPPDATA%\RunnerManager` are intentionally kept stable for now so existing settings, diagnostics and local workflows continue to work.
- Adds the first **Repositories** module without splitting the tool into a second executable.

### Added
- Dedicated **Repositorios** window with persisted local repositories.
- Add a single repository or scan a user-selected folder up to three levels deep; protected and heavy build/dependency folders are skipped.
- Local Git status dashboard: current branch, upstream, working-tree changes, ahead/behind counts, remote `origin`, last commit and repository health state.
- Repository states: `CLEAN`, `CAMBIOS`, `AHEAD`, `BEHIND`, `DIVERGED`, `CONFLICTO`, `SIN REMOTE` and `ERROR`.
- Safe quick actions backed by the Git executable installed on Windows: `Fetch`, `Pull` and `Push`.
- Quick access to repository folder, terminal and GitHub page when `origin` points to GitHub.

### Safety
- GitHub Workspace Manager does not implement Git itself; it invokes `git.exe` with explicit arguments and captures the result.
- Pull uses `git pull --ff-only` and is disabled when the working tree is dirty, the branch has diverged or no upstream exists.
- Push requires explicit confirmation and is blocked when the remote is ahead or the branch has no upstream.
- Repository discovery only scans folders explicitly selected by the user. It does not crawl all disks automatically.
- No repository credentials, GitHub tokens or remote passwords are read or stored by the new module.

## [0.11.0] - 2026-09-09

### Added
- Operational **SALUD** card on the main dashboard: `OK`, `ATENCIÓN`, `ALERTA` or `SIN DATOS` based on current runner state.
- Native Windows desktop notifications through the existing notification-area icon, with no third-party package.
- Optional alerts for job start (`READY → BUSY`), job completion (`BUSY → READY`) and runner `ERROR`/`UNREGISTERED` transitions.
- Notification preferences in Configuración, including an option to notify only when Runner Manager is not currently in the foreground.

### Changed
- The notification-area icon remains available while notifications are enabled, even if close/minimize-to-tray behavior is disabled.
- First state discovery seeds the transition baseline silently so startup does not generate a burst of false notifications.

### Privacy
- Notifications are generated entirely from local runner state. No push service, telemetry or GitHub token is used.

## [0.10.0] - 2026-09-09

### Added
- Native Windows system-tray integration without third-party packages. The tray menu shows a live runner summary plus quick actions for open, refresh, start all, stop all and exit.
- Optional close/minimize-to-tray behavior, enabled by default and persisted in local settings.
- Dedicated **Acerca de** window with version, runtime, OS, local data path, MIT license and the `Neo RS` authorship signature.
- The `Neo RS` footer signature is now interactive and opens the About window from the main and statistics views.

### Changed
- Desktop project explicitly disables SDK implicit usings while enabling WinForms interoperability, avoiding namespace ambiguity between WPF and WinForms.
- README status/version information updated to reflect the current pre-release desktop feature set.

### Privacy
- Tray and About features remain completely local; no telemetry, account login or GitHub token is introduced.

## [0.9.1] - 2026-09-09

### Added
- Firma discreta `Neo RS` en el pie de la ventana principal y del panel de Estadísticas.
- La firma usa una tipografía de sistema de Windows y no añade recursos ni dependencias externas.

## [0.9.0] - 2026-09-09

### Added
- Technical reliability metric (`OK / (OK + failures)`) so user-initiated cancellations do not masquerade as runner failures.
- 24-hour reliability trend against the immediately preceding 24-hour window, with a minimum sample threshold to avoid noisy arrows.
- Per-runner reliability and 24-hour trend in the statistics table.
- Workload-share calculation and a "most active runner" scope signal.

### Changed
- Statistics scope now distinguishes overall success rate (which includes cancelled jobs in the known-result denominator) from technical reliability (which excludes cancellations).
- Statistics window clamps itself to the available Windows work area and runner tables allow horizontal scrolling on narrower displays.

### Accuracy
- Trend is expressed in percentage points and is hidden when either comparison window has fewer than three technically classified jobs.
- Reliability remains derived exclusively from locally retained Worker diagnostics.

## [0.8.0] - 2026-09-09

### Added
- Activity dashboard for the last 14 local days, rendered without external chart dependencies.
- Scope metrics: jobs today, jobs in the last 24 hours, jobs/day, local history coverage, first/last job and peak day.
- Per-day activity tooltips with OK/failure/cancelled breakdown.

### Changed
- Statistics window is now a dashboard rather than only counters and tables.
- Statistics snapshot exposes explicit coverage and daily activity data while preserving the conservative Worker-log audit model introduced in 0.7.5.

## [0.7.5] - 2026-09-09

### Changed
- Audits local statistics at **job** level instead of assuming that superficially similar records should be merged. `Worker_*.log` remains the default per-job source.
- Extracts stable local execution identities when present (`Job ID`, `Plan ID`, `Request ID`, `Timeline ID`).
- Adds conservative duplicate protection: records are only collapsed when the same runner exposes the same stable identity with near-identical start times and no conflicting terminal outcome. Missing IDs are never deduplicated heuristically by timestamp or name.
- Adds an audit strip showing Worker logs scanned, unique local jobs, duplicates discarded and jobs with a stable identity.
- Renames the headline metric from `EJECUCIONES` to `JOBS LOCALES` to avoid confusing runner jobs with GitHub workflow runs.
- Adds average job duration per runner to the statistics table.

### Accuracy
- The dashboard now makes the counting model explicit: a workflow may contain multiple jobs, and statistics describe locally retained runner-job history rather than GitHub workflow-run totals.
- Conflicting known terminal outcomes prevent automatic deduplication even when an upstream identifier is reused.

## [0.7.4] - 2026-09-09

### Fixed
- Corregido el crash al abrir Estadísticas causado por el binding `TwoWay` implícito de `ProgressBar.Value` contra la propiedad de solo lectura pública `ScanProgress`.
- Los bindings de barras de progreso se fijan explícitamente a `Mode=OneWay` para evitar el mismo fallo en futuras pantallas.

## [0.7.3] - 2026-09-09

### Improved
- Statistics now report real scan progress (`X/Y Worker logs`) instead of showing a silent wall of zeroes while the first local-history analysis is running.
- The `EN CURSO` metric is populated immediately from live runner state before historical parsing finishes.
- Historical Worker logs are parsed in a bounded parallel scan (up to four workers) to reduce first-load time without flooding the disk.
- Period filters skip diagnostic files that definitely finished before the requested range, avoiding unnecessary reads.
- Adds a thin progress indicator to the statistics footer while analysis is active.
- Keeps the existing in-memory parsed-log cache, so repeated refreshes in the same session are substantially cheaper.

### Accuracy
- The progress UI distinguishes an active historical scan from a genuine zero-history result. Zero counters during loading are no longer presented as if analysis had completed.

## [0.7.2] - 2026-09-09

### Fixed
- Fixed the statistics view-model build failure by importing the desktop services namespace used by `AppLogger`.
- No behavioral changes to statistics parsing or runner control.

## [0.7.1] - 2026-09-09

### Fixed
- Corrige dos cabeceras de `StatisticsWindow.xaml` que aplicaban `Padding` directamente sobre `Grid`, propiedad no soportada por WPF.
- Las cabeceras de resumen por runner e historial ahora usan `Border` como contenedor de padding.

## [0.7.0] - 2026-09-09

### Added
- Adds a dedicated **Estadísticas** window for the locally retained GitHub Runner history.
- Adds all-time, today, 7-day and 30-day period filters.
- Adds headline metrics for total executions, OK, failures, cancellations, unclassified runs, active jobs and success rate.
- Adds accumulated runtime, average duration and runner-activity scope.
- Adds a per-runner breakdown with total jobs, outcomes, success rate, time spent and latest execution.
- Adds a recent execution history with runner, repository, job, outcome and duration.
- Adds `IRunnerStatisticsService` plus statistics domain models in `RunnerManager.Core`.

### Safety / accuracy
- Statistics are built only from local `_diag/Worker_*.log` files and never read runner credentials.
- Generic diagnostic errors do not automatically mark a job as failed. Only recognised terminal job results classify executions as OK / failed / cancelled; everything else remains `SIN CLASIFICAR`.
- The newest Worker file of a BUSY runner is treated as an active job and excluded from completed-history totals.
- The UI explicitly describes totals as the locally available diagnostic scope rather than claiming complete GitHub lifetime history.

## [0.6.4] - 2026-09-09

### Fixed
- Stops reporting `Preparando job` while the active Worker log already shows a real child process. The progress parser now recognises current `ScriptHandler` variants such as `Which2:`/`Location:` plus `ProcessInvokerWrapper` activity from current GitHub Runner builds.
- Uses active process signals to expose phases such as `Ejecutando PowerShell`, `Ejecutando Node.js`, `Compilando con .NET` or `Sincronizando logs` when explicit `Starting:`/`Finishing:` step markers are unavailable.

### Improved
- Adds a transparent runtime-history fallback. When step boundaries cannot be matched, completed local `Worker_*.log` files can provide a low-confidence percentage based on median total duration instead of leaving every run permanently indeterminate.
- Historical Worker files may use their final diagnostic timestamp as the local completion bound when that runner version does not emit an explicit terminal marker.
- Runtime-only estimates are capped at 95% while BUSY and remain labelled `EST.`; step-weighted history continues to be preferred whenever available.
- Progress metadata now identifies whether an estimate comes from step history or total runner/job duration.

## [0.6.3] - 2026-09-09

### Improved
- Makes the BUSY progress estimator more tolerant of GitHub Runner log variants by recognising additional job-name and job-completion markers.
- Closes a single final open historical step when a trustworthy terminal job marker is present, improving future duration profiles without inventing intermediate timings.
- Adds the current job name above the active phase in the selected-runner progress card.
- Adds a compact progress metadata line in BUSY rows (steps plus elapsed/remaining time) and confidence information in the estimate tooltip.
- Rebalances dashboard columns and disables the unnecessary horizontal table scrollbar at normal supported window widths.

## [0.6.2] - 2026-09-09

### Fixed
- Removed the unsupported `LineHeight` attribute from the WPF log `TextBox`, which caused `MC3072` during XAML compilation.
- Kept log readability through font size and padding without relying on non-supported WPF properties.

## [0.6.1] - 2026-09-09

### Fixed
- Removes the unsupported WPF `TextBlock.LetterSpacing` attribute from the main title, fixing `MC3072` when compiling the .NET 8 WPF application.
- Keeps the title spacing native to Segoe UI instead of relying on a WinUI-only typography property.

## [0.6.0] - 2026-09-09

### Added
- Adds a live `PROGRESO` column for BUSY runners with the current detected phase and an integrated progress bar.
- Adds a larger progress card to the selected-runner detail panel with phase, completed/expected steps, elapsed time and estimated remaining time.
- Adds `RunnerProgressService`, which parses local GitHub Runner `Worker_*.log` diagnostics without requiring a GitHub token or API access.
- Learns from completed local worker logs. A numeric percentage is shown only when the current step sequence matches a comparable completed execution; otherwise the bar remains indeterminate.
- Historical durations are weighted per step using medians, so long build/test/deploy phases contribute proportionally instead of treating every step as equal.
- Slow current steps stretch their expected duration dynamically, preventing an estimated bar from reaching 100% and freezing while work is still running.

### Changed
- BUSY rows are slightly taller to accommodate phase + progress information without crowding the dashboard.
- Dashboard column widths were rebalanced to make room for progress while keeping repository, PID, RAM and uptime visible.
- Progress estimates are explicitly labelled `EST.` and capped below 100% until the runner actually leaves BUSY state.

## [0.5.1] - 2026-09-09

### Fixed
- Reads active GitHub Actions `_diag` log files with `FileShare.ReadWrite | FileShare.Delete`, fixing the live-log error shown when the runner still has its current log open.
- Replaces raw file-lock exceptions in the dashboard with short, user-facing status messages.

### Changed
- Uptime now shows seconds during the first minute and `Xm Ys` during the first hour instead of looking stuck at `0m`.
- Runner mode is localized in the UI (`Interactivo`, `Servicio`, `Desconocido`) while the domain enum remains language-neutral.
- Adds a compact `LIVE` indicator and slightly tighter typography to the integrated log viewer.

## [0.5.0] - 2026-09-09

### Changed
- First dedicated visual pass after the functional .NET rebuild.
- Reworks the dashboard to match the approved dark mockup more closely: denser header, stat cards, icon toolbar, cleaner runner table, richer detail panel and integrated log card.
- Replaces oversized solid state blocks with compact pill badges that include a bright status indicator and subtle border.
- Fixes the selected DataGrid row/cell styling so selection remains dark blue instead of turning white.
- Adds stronger visual hierarchy for runner names and GitHub targets.
- Adds native Segoe MDL2 iconography to toolbar actions and panel headers, removing provisional emoji/unicode action icons.
- Restyles Settings to use the same card-based visual language.

### Added
- `RunnerStateAccentBrushConverter` for bright status dots/borders while retaining darker badge backgrounds.

## [0.4.6] - 2026-09-09

### Fixed
- Fixes the WPF compile error `CS0266` in `SettingsService`: automatic root detection returns `IReadOnlyList<string>` while `RunnerSettings.RunnerRoots` is a mutable `List<string>`.
- The recovered roots are now materialized with `ToList()` before being assigned to settings.

## [0.4.5] - 2026-09-09

### Fixed
- Fixes automatic runner discovery when the app is built under `C:\GitHubRunnerManager\...` while runner folders live directly under `C:\` or another local drive.
- Automatically repairs the stale search root persisted by v0.4.0-v0.4.4 when that root contains no runners.
- The ancestor scan now reaches the drive root instead of stopping one level too early.
- Main and Settings windows explicitly apply the dark foreground/background palette, fixing black text and white surfaces caused by missing inherited window colors.

### Added
- Shallow auto-detection across ready fixed/removable drive roots without recursively scanning disks.
- `Detectar automáticamente` action in Settings.
- Windows immersive dark title bar when supported by the current OS build.

## [0.4.4] - 2026-09-09

### Fixed
- The local build launcher now uses a dedicated PowerShell bootstrap that detects and closes only the previously running `RunnerManager.exe` from the current build output before compiling.
- Prevents MSBuild `MSB3021` / `MSB3027` file-lock failures caused by rebuilding while Runner Manager is still open.
- The PowerShell build bootstrap self-elevates once, allowing it to close an elevated previous test instance safely.

### Changed
- `.NET 8 SDK or later` is accepted by the launcher.
- The launcher reports a clear error if a previous instance cannot be closed instead of letting MSBuild retry ten times.

## [0.4.3] - 2026-09-09

### Fixed
- Fixes the startup crash caused by WPF attempting a default TwoWay binding on the read-only `MainViewModel.LogText` property.
- The log viewer now explicitly binds `TextBox.Text` with `Mode=OneWay`, preserving copy/scroll behavior without writing back into the ViewModel.

## [0.4.2] - 2026-09-09

### Fixed
- Adds explicit desktop-layer global imports for `System.IO` and other base framework namespaces.
- Fixes the 52 compile errors for `Path`, `Directory`, `File`, `DirectoryInfo`, `SearchOption` and `FileNotFoundException` seen during WPF temporary-project compilation.
- Stops relying solely on SDK implicit usings for the WPF build pipeline.

## 0.4.1

- Build launcher now resolves the solution by absolute path.
- Detects execution from inside a ZIP and explains how to fix it.
- Validates the project structure before invoking MSBuild.
- Adds `RUN_ME_FIRST.cmd` and a small diagnostics script.

## [0.4.0] - 2026-09-09

### Added
- First clean .NET 8 / WPF rebuild.
- Core/UI separation.
- Exact process-to-runner matching by executable path.
- Runner states including Ready, Busy, Stopped, Starting, Stopping, Error and Unregistered.
- Interactive runner start/stop/restart.
- Native Windows Service Control Manager support without third-party packages.
- Local settings under `%LOCALAPPDATA%\RunnerManager`.
- Integrated `_diag` log viewer.
- Dark dashboard UI based on the approved prototype.
- Application log under `%LOCALAPPDATA%\RunnerManager\logs`.

### Changed
- PowerShell v0.3 is now treated as a prototype/reference only.
