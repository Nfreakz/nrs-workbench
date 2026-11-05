# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-11-05

- Repository: `Nfreakz/nrs-workbench`; default branch `main`.
- `main` product-code baseline after PR #49: `77445785a8ddd32d43d22c5c21c54a7ca64873df`, app version `0.18.7`.
- PR #47 (CI self-hosted policy), PR #48 (runner safety, Git/settings/logging fixes), and PR #49 (Maintenance scanning/UX) are merged.
- CI #561: SUCCESS on PR #48 merge commit `85380478abeb39e98e173f1519d695bcff48b812`.
- CI #562 was in progress when this handoff was prepared; verify the final conclusion on `77445785...` before choosing any v0.18.7 release candidate. Documentation-only commits may advance `main`.
- v0.18.6 fixed release candidate: `317226dc3e1702341927455435f2675284ea2175`; `Directory.Build.props` reports `0.18.6`; CI #556 SUCCESS.
- Earlier pure v0.18.6 commit `0c0202d0036bc23f23015b1d49b887e1cbbb1726` is superseded by the fixed candidate. Do not tag the old commit.
- v0.18.7 earlier baseline `0e7688ab49276134a00018c27b60281cc7eb0b01` is superseded by later bug/security fixes and Maintenance integration. Do not tag the old commit.
- `qa-preview` still points to the original `0c0202d...` candidate; it is **not** the current fixed release payload.
- Last published release remains [v0.18.5 Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.5). No v0.18.6 or v0.18.7 tag/release existed when checked on 2026-11-05.
- The live GitHub Wiki has not been verified during this update. Do not claim it is current.

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

Integrated and CI-green. Relevant merged PRs: #40, #42, #43, #44, #45 and #49.

Release candidate commit:

`317226dc3e1702341927455435f2675284ea2175`

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
- Maintenance now streams partial scan results so its table does not remain blank while scanning large runner work folders.
- `_work` inventory is bounded per runner, with incomplete measures labeled explicitly; no automatic `_work` deletion.
- Complete detail text wraps across rows and the table indicates whether deletion is safe, affects history or is blocked.

Publication gate still open:

1. Validate Maintenance UI on Windows.
2. Verify Workbench/PREVIEW logs and `_diag` opt-in behavior.
3. Verify `_work` remains inventory-only.
4. Verify BUSY cleanup is blocked.
5. Verify newest Runner/Worker logs survive cleanup.
6. Verify 5 MB rotation and five-archive cap.
7. Verify escaped workFolder and intermediate reparse/junction paths are blocked.

Do not create tag `v0.18.6` until all remaining manual checks pass. When approved, the tag must point to `317226dc3e1702341927455435f2675284ea2175`, not the superseded original candidate. The reported manual preview confirms the initially empty Maintenance table now populates; the destructive safety checks were not independently confirmed in this session.

## v0.18.7 · Portable Workspace

PR #41 is MERGED. Portable Workspace is now integrated in `main`.

Current product-code candidate (recheck CI #562 before approval):

`77445785a8ddd32d43d22c5c21c54a7ca64873df`

This candidate includes the original Portable Workspace changes plus PR #48 safety fixes and PR #49 Maintenance fixes. The previous `0e7688ab...` baseline is no longer the recommended release payload. If later code fixes merge, establish a new candidate and revalidate it.

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

Close the 0.18.x line without reverting the November fixes:

1. Verify CI #562 (and any later main CI) is green on the chosen v0.18.7 commit.
2. Complete the remaining v0.18.6 Windows safety checks against `317226dc...`. User feedback already confirmed the incremental Maintenance scan displays results; other manual cleanup checks remain unconfirmed.
3. If fully validated, create immutable tag `v0.18.6` on `317226dc3e1702341927455435f2675284ea2175` and verify the Windows ZIP prerelease asset.
4. Complete a disposable-runner migration/second-PC test for v0.18.7; CI smoke tests do not replace a real GitHub runner preparation/retry check.
5. If validated, create immutable tag `v0.18.7` on the approved current product-code commit (initial candidate `77445785a8ddd32d43d22c5c21c54a7ca64873df`) and verify the win-x64 prerelease ZIP.
6. Update README, CHANGELOG release dates, HANDOFF and live Wiki after publication. Never claim a release exists merely because a tag or successful CI run exists.
7. Keep GitHub-hosted runners disabled; only repository-scoped self-hosted Windows X64 jobs are allowed.
