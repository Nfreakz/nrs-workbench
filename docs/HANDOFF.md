# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-11-05

- Repository: `Nfreakz/nrs-workbench`; default branch `main`.
- `main` product-code baseline after PR #49: `77445785a8ddd32d43d22c5c21c54a7ca64873df`; the currently CI-validated v0.18.7 code plus release tooling is `9c32e0837e96a4f3535ef7956fdb6583bd95ffb8` (CI #572 SUCCESS), app version `0.18.7`.
- PR #47 (CI self-hosted policy), PR #48 (runner safety, Git/settings/logging fixes), and PR #49 (Maintenance scanning/UX) are merged.
- CI #561: SUCCESS on PR #48 merge commit `85380478abeb39e98e173f1519d695bcff48b812`.
- CI #562 SUCCESS on `77445785...` and CI #572 SUCCESS on `9c32e083...`. A later docs-only main commit may be used after verifying its CI and exact version.
- v0.18.6 fixed release candidate: `317226dc3e1702341927455435f2675284ea2175`; `Directory.Build.props` reports `0.18.6`; CI #556 SUCCESS.
- Earlier pure v0.18.6 commit `0c0202d0036bc23f23015b1d49b887e1cbbb1726` is superseded by the fixed candidate. Do not tag the old commit.
- v0.18.7 earlier baseline `0e7688ab49276134a00018c27b60281cc7eb0b01` is superseded by later bug/security fixes and Maintenance integration. Do not tag the old commit.
- `qa-preview` still points to the original `0c0202d...` candidate; it is **not** the current fixed release payload.
- Latest published release: [v0.18.6 Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.6), with `NRSWorkbench-v0.18.6-win-x64.zip`; recovery Release #21 SUCCESS on 2026-11-05.
- v0.18.7 remains unpublished and has no release tag as of this check.
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

The immutable `v0.18.6` tag points to `317226dc3e1702341927455435f2675284ea2175`, and Release #21 SUCCESS published the win-x64 ZIP on 2026-11-05. Historical manual safety checks beyond the incremental scan behavior were not independently witnessed in this session; do not retroactively claim them verified.

## v0.18.7 · Portable Workspace

PR #41 is MERGED. Portable Workspace is now integrated in `main`.

Current CI-verified candidate including the UTF-8 release-tooling repair (CI #572 SUCCESS):

`9c32e0837e96a4f3535ef7956fdb6583bd95ffb8`

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

- README is updated to state v0.18.6 is published and v0.18.7 remains unreleased.
- Live GitHub Wiki has not been reverified in this handoff.
- Before release, review `CHANGELOG.md`, `docs/TESTING.md`, `docs/RELEASING.md`, `docs/WRITING_STYLE.md` and affected user-facing documentation.

## Next action

1. Keep the published `v0.18.6` tag and ZIP unchanged; the workflow is repaired for UTF-8 release metadata.
2. Before publishing `v0.18.7`, perform the real Portable Workspace second-PC and disposable interactive runner migration checks above. Do not use production runners to establish this gate.
3. Verify latest CI on the approved product+release-tooling commit (initial candidate `9c32e0837e96a4f3535ef7956fdb6583bd95ffb8`; update for a newer documentation-only main commit if needed).
4. If the real migration gate passes, create the immutable `v0.18.7` annotated tag on the exact approved 0.18.7 commit. Verify it does not already exist.
5. Confirm GitHub Release workflow SUCCESS and the published `NRSWorkbench-v0.18.7-win-x64.zip` prerelease asset. Run only on repository-scoped self-hosted Windows X64.
6. Update release date, README and GitHub Wiki to reflect v0.18.7 actually being downloadable; the live Wiki was not yet independently verified.
