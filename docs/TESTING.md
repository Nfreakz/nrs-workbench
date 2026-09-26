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

It also covers portable-settings privacy and round-trip checks plus unambiguous, ambiguous and invalid repository-path relocation. The queue smoke tests cover the connected-runner limit, active-job protection, idle rotation among eligible runners, exclusion of manually stopped runners, persistence of queue-owned stops, confirmation/decline after app restart, restoring only approved runners when disabling the queue, smart CPU/RAM holds, session pause and queue dashboard classification. Catalan resource and portable-language round-trip checks are also included. Review/Cancel UI behavior and ZIP startup must still be checked manually.

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

1. Enable the queue with a limit of 1 and verify the dashboard distinguishes **running**, **next**, **waiting** and **manually stopped** runners.
2. Pause the queue while a runner is BUSY. Verify the job continues and no READY/STOPPED runner is started, stopped or rotated until Resume is pressed.
3. Enable the resource guard and temporarily set a low CPU threshold. While CPU is above it, verify the dashboard reports the hold and no waiting runner starts. Lower host load just below the configured limit and verify the hold remains until CPU clears the 5-point recovery margin; then verify scheduling resumes without manual intervention.
4. Repeat with the RAM threshold and its 5-point recovery margin. Existing BUSY jobs must continue in both cases.
5. Disable the resource guard and verify CPU/RAM no longer block queue starts. Re-enable it and confirm thresholds persist after restarting the app.
6. Export/import portable settings and confirm the resource-guard toggle plus CPU/RAM thresholds round-trip.
