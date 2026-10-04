# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-10-04

- Current `main`: `0e7688ab49276134a00018c27b60281cc7eb0b01`.
- `qa-preview` points to the same commit.
- Application version on `main`: `0.18.7`.
- Open pull requests at verification time: none.
- CI #544 for `0e7688ab...`: SUCCESS.
- Last pure v0.18.6 commit: `0c0202d0036bc23f23015b1d49b887e1cbbb1726`.
- CI #542 for `0c0202d...`: SUCCESS.
- Latest published release: [v0.18.5 Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.5).
- Published Windows asset: `NRSWorkbench-v0.18.5-win-x64.zip`, 71,433,124 bytes, SHA256 `7831f858b9a35de1624465a6ee8b5481220a42a3411bb19cdf41f3dc08cb0a2e`.
- v0.18.6 and v0.18.7 are implemented but not published.
- `docs/VISUAL_SYSTEM.md` remains the visual source of truth.

## Release policy and constraints

- NRS Workbench remains Windows-only. The Linux spike was closed without merge.
- CI and release jobs use self-hosted Windows X64 runners only.
- Do not use GitHub-hosted runners.
- `RUN_ME_FIRST.cmd` remains the normal launcher.
- `RUN_PREVIEW.cmd` remains the disposable preview launcher.
- `qa-preview` is only a movable test pointer, never the release source of truth.
- Do not move or reuse existing tags.
- Releases remain Public Preview / prerelease.
- Do not introduce a custom updater for the GitHub Actions Runner binary.
- Do not start v0.19.0 until v0.18.6 and v0.18.7 are closed correctly.

## v0.18.5 published scope

- Runner discovery validates real runner installation markers instead of relying on folder names.
- Smart Queue can use all detected runners or an explicit selected pool.
- Feedback & Diagnostics provides review-before-share local diagnostic export and GitHub issue/feature entry points without automatic upload.
- Automatic refresh is single-flight, recurring failures are contained/throttled, tray churn is reduced and runtime-session heartbeat records hard/unclean exits.
- Runner Doctor provides read-only, explainable local health checks for installation, registration, processes/services, version and `_diag` activity.
- Runner Doctor does not read credentials, auto-repair, re-register or update runner binaries.

## v0.18.6 · Maintenance Center

Integrated and CI-green. Relevant merged PRs: #40, #42, #43, #44 and #45.

Release candidate commit:

`0c0202d0036bc23f23015b1d49b887e1cbbb1726`

Implemented scope:

- Maintenance Center scans storage before cleanup.
- NRS Workbench logs rotate at 5 MB with at most five retained archives.
- Workbench/PREVIEW log cleanup is low-risk and selected by default.
- Old runner `_diag` cleanup is opt-in.
- Newest Runner and Worker logs are always preserved.
- BUSY runners cannot be cleaned.
- Runner `_work` is measured but never deleted automatically.
- Absolute, escaping or unsafe `workFolder` paths are not traversed.
- Reparse points/junctions anywhere in runner → `_diag` or runner → workFolder chains block inventory and cleanup.
- Cleanup reports files actually removed and bytes actually reclaimed.

Publication gate still open:

1. Validate Maintenance UI on Windows.
2. Verify Workbench/PREVIEW logs and `_diag` opt-in behavior.
3. Verify `_work` remains inventory-only.
4. Verify BUSY cleanup is blocked.
5. Verify newest Runner/Worker logs survive cleanup.
6. Verify 5 MB rotation and five-archive cap.
7. Verify escaped workFolder and intermediate reparse/junction paths are blocked.

Do not create tag `v0.18.6` until these manual checks pass. When approved, the tag must point to `0c0202d0036bc23f23015b1d49b887e1cbbb1726`.

## v0.18.7 · Portable Workspace

PR #41 is MERGED. Portable Workspace is now integrated in `main`.

Merge commit / release candidate:

`0e7688ab49276134a00018c27b60281cc7eb0b01`

Implemented scope:

- `NRSWorkbench.portable` beside the executable activates portable mode.
- Portable data lives under `Data` beside the executable.
- Same-volume runner roots, repository paths, manual runner order and Smart Queue pool paths persist as `@portable/...`.
- Portable paths survive removable-drive letter changes.
- Portable manifest contains no credentials, PATs or registration tokens.
- Portable queue recovery is tied to a hashed machine fingerprint and ignored on another PC.
- Inventory distinguishes Ready on this PC, Needs preparation and pending/retry states.
- Bulk preparation is limited to stopped interactive GitHub.com runners.
- Service runners are outside scope.
- PAT exists only in memory for the preparation session.
- Workbench requests short-lived runner registration tokens and passes only the temporary token to the runner process.
- Preparation preserves runner name, GitHub target, workFolder, runner group, labels, default-label behavior, ephemeral mode and disable-update.
- The whole selected batch is prevalidated before any local runner configuration is removed.
- Online/busy runners, duplicate target/name identities, missing Runner.Listener, unsupported targets, absolute/escaping work folders and reparse paths are blocked.
- Smart Queue must be disabled before preparation.
- GitHub state is revalidated immediately before each local replacement.
- Each runner gets a fresh registration token before replacement.
- Expired/nearly-expired tokens abort before local removal.
- Interrupted migrations are recorded and retry can resolve a remote runner by stable target + name.
- Portable activation creates its marker only after settings/manifest preparation succeeds.
- Ready requires both `.credentials` and `.credentials_rsaparams`.
- Portable refresh/selection is locked while preflight or migration is active.

Publication gate still open:

1. Extract an independent portable test copy.
2. Activate Portable Workspace and confirm `Data` is used after restart.
3. Change drive letter and verify `@portable` paths resolve.
4. Inspect portable files and logs for PAT/token/credential leakage.
5. Move the copy to a second PC and verify Needs preparation.
6. Verify running/service runners cannot be selected for preparation.
7. Prepare one disposable interactive runner with a PAT and verify name/target/workFolder/labels.
8. Simulate a failed migration and verify retry.
9. Verify invalid PAT permissions fail before local runner removal.
10. Verify GitHub online/busy state blocks migration.
11. Verify Smart Queue enabled blocks preparation.
12. Verify missing credential files prevent Ready.
13. Verify corrupt manifest fails closed.
14. Verify reparse points are rejected.
15. Verify a runner changing to online after batch preflight is caught before local removal.

Do not create tag `v0.18.7` until the portable migration path has been manually validated with disposable/test runners.

## Preview testing workflow

- `RUN_ME_FIRST.cmd` updates, builds and runs the normal checkout.
- `RUN_PREVIEW.cmd` keeps the normal checkout on `main`, fetches `qa-preview` into a sibling disposable worktree, builds it and launches only that copy.
- Normal and preview instances are intentionally one-or-the-other, not concurrent.
- Preview uses `%LOCALAPPDATA%\NRSWorkbenchPreview`.
- Preview settings are refreshed from the normal profile, but queue recovery state is not copied.
- Automatic CI ignores pushes to `qa-preview`; validated source branches/PRs remain the source of truth.

## Release automation

`.github/workflows/release.yml` publishes only existing explicit `vX.Y.Z` tags and runs on repository-scoped self-hosted Windows X64.

For each release it:

- checks out the exact tag;
- builds Release;
- runs smoke tests;
- runs the public source audit;
- publishes the win-x64 portable package;
- creates/reuses a GitHub prerelease;
- uploads `NRSWorkbench-vX.Y.Z-win-x64.zip`.

Never move a tag to recover a failed release. The workflow supports manual dispatch against an existing tag.

## Documentation

- README correctly states v0.18.5 is published and v0.18.6/v0.18.7 are integrated but unpublished.
- Live GitHub Wiki has not been reverified in this handoff.
- Before release, review `CHANGELOG.md`, `docs/TESTING.md`, `docs/RELEASING.md`, `docs/WRITING_STYLE.md` and affected user-facing documentation.

## Next action

Close the 0.18.x line before starting v0.19.0:

1. Complete v0.18.6 manual validation against `0c0202d...`.
2. If it passes, tag/publish v0.18.6 from that exact commit.
3. Complete v0.18.7 portable validation against `0e7688ab...` using disposable/test runners.
4. Fix any bug found on a new branch from current `main`.
5. When portable checks pass, tag/publish v0.18.7.
6. Only then start v0.19.0 GitHub Actions Control.
