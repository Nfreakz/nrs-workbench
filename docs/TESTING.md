# Testing

## Local build

On Windows with .NET 8 SDK:

```powershell
dotnet build .\NRSWorkbench.sln -c Release
```

## Git smoke tests

The smoke-test executable uses temporary local repositories and a temporary bare remote. It does not require a GitHub account or network access.

```powershell
dotnet run --project .\tests\NRS.Workbench.SmokeTests\NRS.Workbench.SmokeTests.csproj -c Release
```

It also covers portable-settings privacy and round-trip checks plus unambiguous, ambiguous and invalid repository-path relocation. The queue smoke tests cover the connected-runner limit, active-job protection, idle rotation among eligible runners, the v0.18.3 all-stopped bootstrap, later manual-stop protection, persistence of queue-owned stops, confirmation/decline after app restart, restoring only approved runners when disabling the queue, smart CPU/RAM holds, session pause and queue dashboard classification. Catalan resource and portable-language round-trip checks are also included. Review/Cancel UI behavior and ZIP startup must still be checked manually.

It covers clean/dirty inspection, empty-message protection, partial commits, staged-file protection, unusual filenames, rename/delete handling, ahead/push, behind/fast-forward pull, divergence blocking and remote-credential redaction.

## Public-source audit

```powershell
.\scripts\public-audit.ps1
```

The audit checks for common accidental secrets, private-key material, personal absolute paths in source, generated build artifacts and previously observed invalid WPF properties.

## CI

The Windows CI workflow builds the full solution, runs smoke tests and runs the public-source audit on trusted branch pushes and manual `workflow_dispatch` runs. It intentionally does not execute fork pull-request code on the persistent self-hosted runner.

## Manual migration and distribution checks

On a second Windows PC with some repositories already configured, import a JSON from the first PC and verify the replacement prompt. Test **Cancelar** (no saved change), **No** (retain local registrations), and **Sí** (replace only registrations, not folders). Verify missing-path notices and **Guardar** semantics. After an approved tag, verify the extracted ZIP can start and run on a normal Windows desktop; CI only inspects the archive structure and payload.

## One-command readiness check

```text
RUN_PUBLIC_READINESS.cmd
```

This performs the Release build, Git smoke tests and public-source audit in sequence.

## v0.18.2 manual safety checks

1. With one READY and one manually STOPPED runner, enable the queue; verify the stopped runner stays stopped while the queue operates.
2. With two READY runners, enable the one-runner queue, let it stop one, and exit NRS Workbench explicitly. Restart; the confirmation dialog must list previously queue-managed folders. Selecting **No** must not start them.
3. Repeat and select **Yes**; verify only the approved queue-managed runner can restart. If the queue is disabled before restarting, the confirmation must still be required before any restoration.
4. With an active BUSY job, enabling the queue must never stop it. Test service-installed and interactive runners separately on a Windows PC.
5. Select **Català** in Settings, save and restart. Verify main window, Settings, repositories, statistics, queue status and critical Git confirmation dialogs. Confirm Spanish and English remain selectable. The language setting must round-trip in portable JSON.
6. Verify `%LOCALAPPDATA%\\NRSWorkbench\\runner-queue-state.json` is local-only and never appears in the portable JSON; do not publish paths or contents from a real machine.


## v0.18.3 manual smart-queue checks

1. Stop every runner, enable the queue with a limit of 1, and verify it starts one candidate automatically without disabling the optimizer first. With limit 2, it must work toward two connected candidates without exceeding the configured limit.
2. With the queue active, select a stopped runner and verify **Start** remains available as an explicit opt-in. **Stop**, **Restart**, **Start all** and **Stop all** remain guarded while the queue manages capacity.
3. After a bootstrapped runner has reached READY, stop it manually/outside the queue and verify it is not reclaimed automatically; another eligible candidate may be started instead.
4. After restarting with queue-owned stops in the recovery journal, choose **No** in the recovery prompt. Even if every runner is stopped, the declined runner must remain excluded from automatic bootstrap during that app session.
5. Enable the queue with a limit of 1 and verify the dashboard distinguishes **running**, **next**, **waiting** and **manually stopped** runners.
6. Pause the queue while a runner is BUSY. Verify the job continues and no READY/STOPPED runner is started, stopped or rotated until Resume is pressed.
7. Enable the resource guard and temporarily set a low CPU threshold. While CPU is above it, verify the dashboard reports the hold and no waiting runner starts. Lower host load just below the configured limit and verify the hold remains until CPU clears the 5-point recovery margin; then verify scheduling resumes without manual intervention.
8. Repeat with the RAM threshold and its 5-point recovery margin. Existing BUSY jobs must continue in both cases.
9. Disable the resource guard and verify CPU/RAM no longer block queue starts. Re-enable it and confirm thresholds persist after restarting the app.
10. Export/import portable settings and confirm the resource-guard toggle plus CPU/RAM thresholds round-trip.

## v0.18.4 manual repository-guidance checks

1. Select CLEAN, CHANGES, AHEAD, BEHIND, DIVERGED/CONFLICT and no-remote repositories and verify the right-side guidance changes without modifying Git state.
2. Run Fetch, Pull and Push from NRS Workbench and confirm each completed/failed operation appears in the session activity list with repository name and timestamp.
3. Complete a selective Commit and a local branch switch; verify both are added to the same in-memory activity list.
4. With a Pull-enabled repository selected, create a local edit outside NRS Workbench before pressing Pull. Pull must be blocked after the service re-inspects the working tree.
5. With a Push-enabled repository selected, switch branch outside NRS Workbench before pressing Push. Push must be blocked and ask for a refresh.
6. Close and reopen NRS Workbench and confirm repository action history is empty; it is intentionally session-only and not telemetry/persistent audit history.


## v0.18.5 runner discovery and queue-pool checks

1. Create a valid runner installation under a configured root with a folder name that does not start with `actions-runner`; verify NRS Workbench detects it.
2. Create an empty folder whose name starts with `actions-runner`; verify it is ignored.
3. In Settings, disable **Use all detected runners**, select only a subset, save, and verify only those runners appear as Smart Queue participants.
4. Keep an excluded runner READY while the queue limit is 1; verify the queue does not stop it or count it against the selected pool.
5. Select a stopped runner while an excluded runner is already online; verify Smart Queue may start the selected runner without touching the excluded one.
6. Change the selected pool while NRS Workbench remains open and verify newly selected runners become eligible without automatically controlling runners removed from the pool.
7. Export/import portable settings and verify the queue selection mode and selected runner paths round-trip without credentials.

8. With a BUSY runner visible in the main table, verify the current action, compact step/time metadata and estimated percentage share one line above the progress bar. Confirm 40 px rows and the shorter header do not clip status pills, text or the progress bar.

9. Right-click a runner row and verify that row becomes selected before the context menu opens. The menu must use the graphite dark theme with readable text and a neutral hover state, not the native white/blue Windows menu. Check Start, Stop and Restart enablement matches the selected runner state and Smart Queue safety rules; Folder, Diagnostics and GitHub actions must target the right-clicked runner.

10. Disable Smart Queue in Settings and verify the Smart Queue dashboard disappears completely and the runner table expands into the freed vertical space. Re-enable Smart Queue and verify the dashboard returns with its current state.

11. Resize the lower Details/Log area by dragging the horizontal splitter. Double-click it to collapse the panel and double-click again to restore it; the runner table must use the freed height without layout gaps.
12. At 100% Windows scaling, verify the compact header, toolbar, search/sort row and host-resource strip remain readable and do not clip text or controls. The runner table should show more rows than before without reducing body text below the visual-system minimum.
13. Select several runner rows and verify the selection uses a restrained graphite highlight rather than a strong blue fill. Confirm PID, RAM, uptime and version remain readable in the narrower metadata columns.

14. With runners on two different runner versions, verify the most common version stays muted while the uncommon version is highlighted in amber. With a single version across all runners, all version values should remain muted.

15. In the lower runner pane, verify there is no redundant "Runner details" heading. Status plus folder/GitHub quick actions must sit beside the runner name, metadata must use two compact columns, and both Details and Log must share the same shorter default height without clipping BUSY progress information.

16. Open Settings and verify the Buy Me a Coffee button is visible in the fixed top header without scrolling. The old support card must not remain lower in the settings list.
17. With one or more BUSY runners, verify the progress percentage and the Mode column have a clear visual gap at 100% scaling and do not read as one combined value.


## v0.18.5 Feedback & Diagnostics manual checks

1. Open **Settings → Feedback & diagnostics** and verify the window uses the existing graphite visual system and remains usable at normal Windows scaling.
2. Use **Report an issue** and **Suggest improvement**. Each action must open the matching GitHub issue template in the browser; NRS Workbench must not attach or upload any diagnostics.
3. Review the diagnostics preview. It must show environment/configuration counts without raw runner roots, repository paths, remote URLs, settings.json or runner `_diag` contents.
4. Add a synthetic token-shaped string and a configured local path to the application log in a test environment; refresh the preview and verify both are redacted. Also verify Windows user/machine identity is not exposed.
5. Save the diagnostics ZIP. Verify it contains only `diagnostics.txt` and `README.txt`, and that the displayed preview matches `diagnostics.txt`.
6. Cancel the Save dialog and verify no file is created and no network request is made.
7. Trigger a controlled UI exception in a development build and verify the error is logged locally and the user can choose to open Feedback & diagnostics. Declining must simply close the error prompt.


## Preview worktree launcher

For manual feature testing, keep the normal checkout on `main` and run `RUN_PREVIEW.cmd`.

The launcher fetches `origin/qa-preview`, creates or refreshes a sibling disposable worktree, closes any running NRS Workbench instance, copies the normal settings into the isolated preview profile, builds Release and launches the preview. The normal checkout is never switched to the feature branch.

Manual checks:
1. Run `RUN_ME_FIRST.cmd` and verify the normal window title is `NRS Workbench`.
2. Run `RUN_PREVIEW.cmd`; verify the normal checkout remains on `main` and the preview window title is `NRS Workbench · PREVIEW`.
3. Change a harmless setting in preview, exit, then reopen normal. The normal setting must be unchanged.
4. Re-run preview after moving `qa-preview`; the existing worktree must move to the new preview HEAD without a branch switch in the normal checkout.


## v0.18.5 long-running runtime-resilience checks

1. Keep the preview running across repeated automatic refresh intervals while forcing one refresh cycle to take longer than the configured interval. Verify later ticks are skipped rather than queued and the UI remains responsive.
2. In a development/test build, force the automatic refresh path to throw repeatedly. Verify the failure is logged locally but does not open one modal error window per timer tick.
3. Force repeated unhandled UI exceptions inside the one-minute cooldown. Verify only the first opens the Feedback & diagnostics prompt; later exceptions remain logged/throttled instead of creating a dialog storm.
4. Verify a later successful automatic refresh clears the skipped-cycle counter and normal periodic operation continues.


## Runtime session marker / hard-exit diagnostics

1. Start preview and verify `%LOCALAPPDATA%\NRSWorkbenchPreview\runtime-session.json` is created without repository paths, runner paths or credentials.
2. Leave preview running for more than one minute and verify `LastHeartbeatAt` advances while `StartedAt` remains unchanged.
3. Exit normally through NRS Workbench and verify the runtime-session marker is removed and the local app log records a clean shutdown.
4. In a development test only, terminate the process externally. Restart preview and verify the local app log records the previous session as an unclean shutdown with start and last-heartbeat timestamps.


## v0.18.5 Runner Doctor manual checks

1. Open **Runner Doctor** from the main toolbar and verify the window follows the graphite visual system, remains readable at normal Windows scaling and does not crowd the main dashboard.
2. Verify each detected runner appears once with **OK**, **Attention** or **Problem**, plus state, mode, version, latest local activity and a short summary.
3. Select a healthy interactive runner and verify installation, `.runner`, listener/worker, version and `_diag` checks explain why the runner is healthy.
4. Stop a healthy runner intentionally and verify **STOPPED** is not classified as a fault by itself.
5. For a service-installed runner, verify Runner Doctor reports the Windows service and its current native state. A missing configured service must become **Problem**.
6. On a safe test copy, remove or rename one installation marker such as `run.cmd` or `bin/Runner.Listener.exe`; verify the runner becomes **Problem** without Runner Doctor attempting any repair.
7. Verify a missing `.runner` registration marker becomes **Problem** and the doctor never reads `.credentials` or `.credentials_rsaparams`.
8. With a BUSY runner, verify an active Worker process is reported. A BUSY snapshot without a Worker process should produce **Attention** rather than performing any action.
9. Verify **Open folder** and **Open _diag** target only the selected runner. Missing `_diag` must show an informational message rather than failing.
10. Use **Copy summary** and verify the copied text contains the selected runner diagnosis but passes through the existing sensitive-data redactor.
11. Press **Refresh diagnosis** while runner states change and verify the list updates without starting, stopping, registering or reconfiguring any runner.


## v0.18.6 Maintenance Center manual checks

1. Open **Maintenance** from the main toolbar and verify scanning does not block the main UI.
2. Confirm Workbench logs and PREVIEW logs show measured and reclaimable sizes separately.
3. Confirm each runner `_diag` row preserves its newest Runner and Worker log regardless of age.
4. With a BUSY runner, confirm its diagnostic row is inventory-only and reports zero reclaimable bytes.
5. With a stopped runner and synthetic logs older than the selected cutoff, select its diagnostic cleanup and verify the confirmation warns that statistics/progress history will shrink.
6. Run cleanup and confirm only the reviewed files are deleted; runner credentials, `.runner`, repositories and `_work` remain untouched.
7. Verify `_work` sizes appear as **Inventory only** and cannot be selected for deletion.
8. Grow the local Workbench log past 5 MB in a test profile and verify rotation creates numbered archives while retaining no more than five.


## v0.18.7 Portable Workspace manual checks

1. On a disposable extracted copy, open **Settings → Portable workspace** and activate portable mode. Verify `NRSWorkbench.portable` and `Data/settings.json` appear beside the executable, then restart.
2. Confirm portable mode uses `Data` instead of `%LOCALAPPDATA%\\NRSWorkbench`.
3. Put configured runner roots/repositories on the same removable volume, change its Windows drive letter, reopen Workbench and confirm the paths resolve correctly without manual edits.
4. Inspect `Data/settings.json`, `portable-workspace.json` and `runner-queue-state.json`: same-volume paths may use `@portable/`, but none may contain PATs, runner registration tokens, `.credentials` contents or the raw Windows machine name/MachineGuid.
5. Move the workspace to a second PC and verify a Smart Queue journal from the first PC is ignored rather than offering automatic runner recovery.
6. Verify already prepared runners for the current PC show **Ready on this PC** and are not selected for migration.
7. Verify a running interactive runner cannot be prepared until it is stopped. Verify service-installed runners show unsupported/not portable and cannot be selected.
8. Using a disposable test runner, provide a PAT only in the Portable Workspace window and prepare it. Verify the runner keeps its name, GitHub target, work folder and custom labels and can start normally afterwards.
9. After preparation, search the portable `Data` directory and application logs for the PAT and temporary registration token; neither may be present.
10. Interrupt/fail a disposable migration after local removal and verify the runner appears as **Migration pending · retry**. Retry with a fresh GitHub token and confirm the stored non-secret metadata is sufficient to complete registration.
11. Close the Portable Workspace window after entering a PAT without running migration, reopen it and verify the password box is empty.
12. Confirm no migration starts merely because the external drive was inserted or because Workbench detected another machine.


## v0.18.7 Portable Workspace checks

1. On an extracted test copy, activate **Portable Workspace** while the runners on the same external volume are registered and working on the current PC. Restart and verify data is read from the adjacent `Data` directory.
2. Change the removable-drive letter and verify same-volume runner roots, repository paths, manual runner order and Smart Queue pool paths resolve correctly without editing settings.
3. Verify PREVIEW still uses `%LOCALAPPDATA%\NRSWorkbenchPreview` and does not activate portable mode from the disposable preview worktree.
4. Move the portable workspace to another test PC. Stopped interactive runners should show **Needs preparation**; service-installed or running runners must not be selectable.
5. Enter a GitHub access token with insufficient permissions and select several runners. The bulk preflight must fail before any `.runner` file is removed.
6. With valid permissions, prepare multiple disposable/test runners. Verify the runner name, target, runner group, work folder, custom labels and default-label behavior are preserved.
7. Verify two selected folders with the same GitHub target and runner name are blocked before migration starts.
8. Verify a missing `bin\Runner.Listener.exe`, a non-github.com target, an absolute workFolder or a workFolder escaping the runner directory is not eligible for preparation.
9. Interrupt a migration after local removal in a disposable test runner, reopen Workbench and verify the pending non-secret manifest allows retry with a fresh GitHub token.
10. Inspect `Data\settings.json`, `portable-workspace.json`, local logs and queue/runtime state. They must not contain the GitHub access token, runner registration token, `.credentials` contents, raw MachineGuid or raw machine name.
11. During preflight/preparation, Refresh and runner selection are disabled; closing the window cancels the active operation and clears the password field.
12. If GitHub reports a selected runner as `online` or `busy`, preflight must stop before local configuration is removed; stop the old host and retry after GitHub reports it offline.
13. Remove either `.credentials` or `.credentials_rsaparams` from a disposable prepared runner and verify Portable Workspace no longer reports it as ready for this PC.
14. Simulate an expired/nearly expired registration token and verify preparation stops before `remove --local` is executed.
15. With Smart Queue enabled in saved Settings, Portable Workspace must refuse preparation before GitHub preflight or local runner changes begin. Disable/save Smart Queue and retry.
16. In a disposable copy, simulate an activation failure while writing portable manifest data and verify `NRSWorkbench.portable` is not created; the next launch must remain in normal mode.
17. Activate portable mode with one runner missing `.credentials` or `.credentials_rsaparams`; it must not be recorded as already prepared for the current PC.
18. In a multi-runner disposable batch, let preflight complete while all runners are offline, then make a later runner appear online/busy before its turn. Its final pre-mutation revalidation must block it without removing its local `.runner`.
19. Retry a pending migration whose original numeric runner ID is stale but whose target + runner name still identify the remote runner. Preflight must resolve it by name, recheck offline/busy state and continue only when the match is unique.
20. Let an early batch runner take long enough that a token minted during batch preflight would be near expiry. The later runner must request a fresh registration token immediately before local replacement.
21. Corrupt `Data\portable-workspace.json` in a disposable copy. Portable runner preparation must fail closed with a visible error instead of treating the manifest as empty or overwriting pending migration state.
22. Inject an absolute/out-of-volume runner path into a disposable manifest and verify it is rejected. Runner state persisted by Portable Workspace must use `@portable/` paths only.
23. Point a disposable runner folder, `bin`, `Runner.Listener.exe` or existing workFolder through a Windows reparse point. Portable preparation must refuse it before local configuration removal.


## v0.18.6 maintenance checks

1. Open **Maintenance** and scan with stopped runners. Confirm Workbench/PREVIEW logs are low-risk, old runner `_diag` is opt-in and `_work` is inventory-only.
2. Start a runner after the scan but before pressing **Clean selection**. Historical diagnostic cleanup for that runner must be skipped and reported separately from failures.
3. With a stopped test runner, create old/new `Runner_*.log` and `Worker_*.log` files. After scanning, change which Worker file is newest; cleanup must preserve the newest Runner and Worker again at deletion time.
4. A `_diag` location that is a Windows reparse point must remain inventory-only and never become cleanable.
5. Candidate files outside the exact reviewed directory must never be deleted.
6. Verify the application log rotates at 5 MB and keeps at most five archives.
7. Configure a disposable runner with an absolute or `..`-escaping workFolder, or a workFolder root that is a reparse point. Maintenance must not traverse it and must report zero measured/reclaimable bytes.
8. On a disposable runner, place a Windows junction/reparse point in an intermediate path segment leading to `_diag` or the configured workFolder. Maintenance must report zero measured/reclaimable bytes for that location and must not enumerate through the junction.

