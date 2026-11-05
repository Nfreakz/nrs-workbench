# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-11-05

- Independent app/repo: `Nfreakz/nrs-workbench`, default branch `main`.
- **Last published release: [v0.18.7 Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.7)**, published 2026-11-05. Asset `NRSWorkbench-v0.18.7-win-x64.zip` (71,695,098 bytes; SHA256 `0C3F182DC29502B16C8739FFFA96E554F93815335E0B9783307994A89CA728FC`).
- Tag `v0.18.7` is an **annotated immutable tag** resolving to `70229481c01d4de469f9007ecd9938fa0be92c48` (app version `0.18.7`). Do not move it.
- GitHub [Publish v0.18.7 #1](https://github.com/Nfreakz/nrs-workbench/actions/runs/42000544874): **SUCCESS** on the local self-hosted Windows X64 runner. Build, smoke tests, source audit, ZIP verification, tag/release creation and upload all passed.
- The temporary publication workflow was retired in PR #56. Normal future releases use `.github/workflows/release.yml` on the same repository-scoped self-hosted Windows X64 runner.
- Previous [v0.18.6 Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.6): published with win-x64 ZIP; Release #21 SUCCESS. Do not move its tag.
- PRs #47–#49 (CI policy, security fixes, Maintenance UI), #51 (PowerShell UTF-8 release JSON), #52 (release docs), #53 (handoff) and #54/#56 (temporary publisher and retirement) are merged.
- Last observed `main` before this docs closure: `54ffe298e3cb07bd61744ed1d38f1701eecb9320`; a docs merge will advance it. Verify live HEAD and CI when continuing work.
- **Physical cross-PC portable runner preparation is still unverified in an independently observed manual test.** The release description and README explicitly warn users to test only with disposable stopped interactive runners. Do not reinterpret successful smoke tests as proof of physical migration.
- The [GitHub Wiki](https://github.com/Nfreakz/nrs-workbench/wiki) was updated on 2026-11-05 through one-time self-hosted Windows workflow [Wiki #2](https://github.com/Nfreakz/nrs-workbench/actions/runs/42007741452), **SUCCESS**. Remote Wiki HEAD `39e75bdc30b4cc14eadc96781bf9ff506343588c` was verified after the push. Home received the v0.18.7 latest-release block, with versioned Portable Workspace and Maintenance Center pages and sidebar links. A rendered browser click-through remains independently unverified.
- No new code changes are needed to consider 0.18.7 published. Any later user-reported migration bug should be fixed in a new, properly versioned release instead of silently modifying the published tag.

## Publication evidence and outstanding follow-up · 2026-11-05

- GitHub Release: https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.7
- Download: https://github.com/Nfreakz/nrs-workbench/releases/download/v0.18.7/NRSWorkbench-v0.18.7-win-x64.zip
- Release workflow: https://github.com/Nfreakz/nrs-workbench/actions/runs/42000544874 (SUCCESS).
- Annotated tag object `4d7375814f8d2876a85061112ccb3db9d59558ee` targets commit `70229481c01d4de469f9007ecd9938fa0be92c48`.
- The ZIP was created and uploaded successfully; downloaded startup on a separate Windows PC is not yet independently verified.
- Next independent tasks: physical portable migration QA with a disposable runner, visual Wiki click-through review when browser access is available, and v0.19.0 design.

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
- v0.18.6 and v0.18.7 are published; v0.19.0 may be planned next. Keep outstanding portable physical QA visible.

## v0.18.5 published scope

- Runner discovery validates real runner installation markers instead of relying on folder names.
- Smart Queue can use all detected runners or an explicit selected pool.
- Feedback & Diagnostics provides review-before-share local diagnostic export and GitHub issue/feature entry points without automatic upload.
- Automatic refresh is single-flight, recurring failures are contained/throttled, tray churn is reduced and runtime-session heartbeat records hard/unclean exits.
- Runner Doctor provides read-only, explainable local health checks for installation, registration, processes/services, version and `_diag` activity.
- Runner Doctor does not read credentials, auto-repair, re-register or update runner binaries.

## v0.18.6 · Maintenance Center

Published and CI-green. Relevant merged PRs: #40, #42, #43, #44, #45 and #49.

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

Post-release additional manual coverage (not independently witnessed in this session):

1. Validate Maintenance UI on Windows.
2. Verify Workbench/PREVIEW logs and `_diag` opt-in behavior.
3. Verify `_work` remains inventory-only.
4. Verify BUSY cleanup is blocked.
5. Verify newest Runner/Worker logs survive cleanup.
6. Verify 5 MB rotation and five-archive cap.
7. Verify escaped workFolder and intermediate reparse/junction paths are blocked.

The immutable `v0.18.6` tag points to `317226dc3e1702341927455435f2675284ea2175`, and Release #21 SUCCESS published the win-x64 ZIP on 2026-11-05. Historical manual safety checks beyond the incremental scan behavior were not independently witnessed in this session; do not retroactively claim them verified.

## v0.18.7 · Portable Workspace

PR #41 is MERGED. Portable Workspace is published in v0.18.7 Public Preview.

Published release commit, tag `v0.18.7` (Publish #1 SUCCESS):

`70229481c01d4de469f9007ecd9938fa0be92c48`

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

Post-publication manual test gap (unverified, use disposable runners only):

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

The `v0.18.7` tag and Public Preview are already published; the user approved release with an explicit warning that physical second-PC migration tests are still outstanding. Never move the tag to address subsequent issues.

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

- Docs PR #55 updates README, CHANGELOG, ROADMAP, HANDOFF and adds `docs/PORTABLE_WORKSPACE.md` for v0.18.7. Verify it has merged before beginning a new task.
- `docs/WIKI_UPDATE_0.18.7.md` records the completed automated Wiki update (Wiki #2 SUCCESS and remote SHA verification), with rendered-link visual inspection still open.
- Future changes must preserve `docs/WRITING_STYLE.md`, `docs/RELEASING.md` and `docs/TESTING.md` guidance.

## Next action after v0.18.7

1. **Publication closed:** immutable v0.18.7 tag, Public Preview, Windows x64 ZIP and released documentation are verified. The live GitHub Wiki update ran on the repository-scoped self-hosted Windows runner: Wiki #2 SUCCESS; remote SHA `39e75bdc30b4cc14eadc96781bf9ff506343588c`. No temporary publisher should remain after the retirement PR.
2. **Manual QA not yet witnessed:** test Portable Workspace on an independent second Windows PC, with a disposable *stopped interactive* GitHub.com runner. Validate drive-letter changes, runner registration metadata, recovery, busy/service exclusions and absence of PAT/token persistence. Do not use production runners for the first migration trial.
3. **Visual documentation check:** open the live GitHub Wiki Home, Portable Workspace and Maintenance Center pages in a real browser to confirm rendering and links. The update workflow verified the remote Git commit but did not inspect the rendered UI.
4. **v0.19.0 GitHub Actions Control:** start with read-only workflows/runs and least-privilege auth design. Add explicit, confirmed dispatch/rerun/cancel only after safety and permission gates; use self-hosted Windows X64 only.
5. Audit cold startup, idle CPU/RAM, long-running refresh, Smart Queue stability and larger Maintenance inventories using real data, without inventing performance claims.
6. Refresh the product screenshot only after a non-sensitive actual v0.18.7+ capture. Existing screenshot is correctly labeled v0.18.4.
7. Keep each NeoRS app's repository, tags, credentials, runners and handoff separate. Never move or recreate the published tags.
