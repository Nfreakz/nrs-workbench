# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-10-04

- Latest published release: [v0.18.5 Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.5).
- Published Windows asset: `NRSWorkbench-v0.18.5-win-x64.zip`, 71,433,124 bytes, SHA256 `7831f858b9a35de1624465a6ee8b5481220a42a3411bb19cdf41f3dc08cb0a2e`.
- v0.18.5 release source is the tag created from the release-prepared main line; the release workflow published the prerelease successfully on 2026-10-04 local time.
- Current `main`: `c3d86a1035bc29957cd2838f96f3ff8b49196a67`; v0.18.6 Maintenance Center is integrated on `main` but not published.
- v0.18.5 includes runner discovery by real installation markers, explicit Smart Queue pools, Feedback & Diagnostics, long-session runtime hardening and Runner Doctor.
- `main` is protected by repository rules requiring pull requests and blocking deletion/force-push.
- `docs/VISUAL_SYSTEM.md` remains the visual source of truth.
- PR #40 (v0.18.6 Maintenance Center) is merged. Active development is PR #41 / `feat/v0.18.7-portable-workspace`, rebased cleanly on current `main`.

## v0.18.5 published scope

- Runner discovery validates real runner installation markers instead of relying on folder names.
- Smart Queue can use all detected runners or an explicit selected pool; excluded runners are not controlled or counted.
- Feedback & Diagnostics provides review-before-share local diagnostic export and GitHub issue/feature entry points without automatic upload.
- Automatic refresh is single-flight, recurring failures are contained/throttled, tray churn is reduced and runtime-session heartbeat records hard/unclean exits.
- Runner Doctor provides read-only, explainable local health checks for installation, registration, processes/services, version and `_diag` activity.
- Runner Doctor does not read credentials, auto-repair, re-register or update runner binaries.

## Release documentation

README now reflects the published v0.18.5 ZIP. The preview image uses fictional runner, repository, path and log data. The live GitHub Wiki still requires an explicit review in a Wiki-capable session before it can be claimed current. Before each release, read `docs/WRITING_STYLE.md`, review affected documentation and distinguish released features from development work.

## Future roadmap

- v0.18.5: published Public Preview baseline.
- v0.18.6: Maintenance Center and storage hygiene integrated on `main`, unreleased.
- v0.18.7: Portable Workspace and reviewed bulk preparation for interactive runners, active in PR #41.
- v0.19.0: GitHub Actions Control after the 0.18.x maintenance/portability line is closed.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.


## Preview testing workflow

The normal checkout remains on `main` and is the day-to-day NRS Workbench copy.

- `RUN_ME_FIRST.cmd` updates, builds and runs the normal checkout.
- `RUN_PREVIEW.cmd` never switches the normal checkout. It fetches the disposable remote `qa-preview` channel into a sibling Git worktree named `<repo>-preview`, builds it and launches only that copy.
- The launchers close an existing NRS Workbench process first. Normal and preview are intentionally one-or-the-other, not concurrent.
- Preview uses `%LOCALAPPDATA%\NRSWorkbenchPreview`. Before launch, its `settings.json` is refreshed from the normal profile so real runner/repository registrations can be tested without persisting preview changes into the normal profile. The queue recovery journal is not copied.
- `qa-preview` is only a movable test pointer. It is not a release branch and never replaces the feature branch/PR as source of truth.
- automatic CI ignores pushes to `qa-preview`; the source feature branch/PR remains the validated commit, while `workflow_dispatch` stays available if a manual preview-channel run is ever needed.


## v0.18.6 integrated state

PR #40 is merged into `main`.

Scope:
- application version on `main` is 0.18.6 until v0.18.7 merges;
- Maintenance Center scans storage before cleanup;
- NRS Workbench logs rotate at 5 MB with five retained archives;
- Workbench/PREVIEW log cleanup is low-risk and selected by default;
- old runner `_diag` cleanup is opt-in because Worker logs feed statistics and progress estimation;
- newest Runner/Worker logs are always preserved and BUSY runners are blocked from diagnostic cleanup;
- runner `_work` is measured but intentionally not deleted automatically;
- cleanup results count only files actually cleaned and bytes actually reclaimed.

v0.18.6 is integrated but not published.

## v0.18.7 development state

Branch: `feat/v0.18.7-portable-workspace`, rebased directly on current `main`.

Scope:
- application version is 0.18.7 on this branch;
- `NRSWorkbench.portable` beside the executable activates the portable profile on the next launch;
- portable data is stored under `Data` beside the executable;
- same-volume runner roots, repository paths, manual runner order and Smart Queue pool paths are persisted as `@portable/` relative tokens and resolved against the current volume root;
- portable Smart Queue recovery state is tied to a hashed machine fingerprint and ignored on another PC;
- portable manifest records prepared/pending runners without credentials, access tokens or temporary registration tokens;
- bulk preparation is limited to stopped interactive GitHub.com runners;
- the GitHub access token exists only in the window/session; Workbench requests a short-lived registration token and passes only that token to the runner process via `ACTIONS_RUNNER_INPUT_TOKEN`;
- runner migration uses local removal plus unattended configure/replace while preserving name, URL, work folder, runner group, custom labels, default-label behavior, ephemeral mode and disable-update mode;
- a pending manifest record is written before local removal so a failed migration can be retried;
- the complete selected batch is prevalidated against GitHub before any local configuration is removed;
- duplicate GitHub target/name identities, missing Runner.Listener binaries, unsupported targets and unsafe/non-relative work folders are blocked before preparation;
- the Portable Workspace grid and refresh action are locked while a preflight/preparation operation is active.

Manual preview cannot turn the disposable PREVIEW worktree itself into portable mode. Activation must be checked on an extracted test copy. Do not publish v0.18.7 until its exact-head CI passes and portable migration is manually validated on disposable/test runners.
