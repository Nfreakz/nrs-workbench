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

## Publication status and handoff checkpoint · 2026-11-05

Verified from GitHub after the user requested to publish v0.18.7 and move work to a new chat:

- **v0.18.6 is actually published**: [Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.6), asset `NRSWorkbench-v0.18.6-win-x64.zip`. Recovery Release #21: SUCCESS. The first Release #20 had failed when PowerShell 5.1 sent non-ASCII release-note JSON without UTF-8; PR #51 fixed it and CI #572 succeeded.
- **v0.18.7 is NOT published yet**: no remote `refs/tags/v0.18.7`, no GitHub release or downloadable asset when last checked. Do not announce it as available.
- Release-ready code, latest incorporated docs: `70229481c01d4de469f9007ecd9938fa0be92c48`, `Directory.Build.props` version `0.18.7`. This is a docs-only successor of `9c32e0837e96a4f3535ef7956fdb6583bd95ffb8` (CI #572 SUCCESS). CI #577 on the documentation branch: SUCCESS; CI #578 on `70229481...` was running at the checkpoint; confirm its final outcome before tagging.
- The code baseline includes Portable Workspace, runner/Git/settings safety fixes from PR #48, responsive Maintenance UI from PR #49, and UTF-8 release tooling from PR #51. PR #52 updated README/CHANGELOG/ROADMAP after the actual v0.18.6 publication.
- Manual **Portable Workspace migration on a second PC with a disposable interactive test runner** is not supported by evidence in this chat/repository. There are extensive automated smoke tests, but no real external migration check. The release policy explicitly says not to tag v0.18.7 until this manual test is complete.
- **No GitHub Actions hosted runners**. CI and release remain on repository-scoped `[self-hosted, Windows, X64]`; no Actions artifacts used as distribution.
- The connected GitHub app can edit code and merge PRs, but does not expose an action to create an annotated tag or manually dispatch a workflow. A user with a local Git checkout may run the two tag commands, after passing the manual gate and verifying current SHA and remote tag absence.
- GitHub Wiki was not verified; update it separately after v0.18.7 release. Keep docs factual.
- Repo health at checkpoint: no open PRs or issues. The new chat must refresh live status, as release or CI may have changed.

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

## Next action: finish v0.18.7 then work on backlog

### Blocker A: manual release gate

Do **not** experiment with existing/production runners. Use a standalone copied application + disposable interactive runner:

1. Test `NRSWorkbench.portable` activation and adjacent `Data` directory; restart the copy and verify it uses portable data instead of normal `%LOCALAPPDATA%`.
2. Change the volume's drive letter and check same-volume runner/repository references and Smart Queue configuration still resolve.
3. On a **second PC**, ensure stopped test runners are marked `Needs preparation`; active or service runners are blocked.
4. Prepare one disposable offline interactive GitHub.com runner with an authorized short-lived PAT session, checking target/name/labels/workFolder and ability to run afterward. Do not use production credentials or tokens in logs/screenshots/chats.
5. Verify invalid/insufficient permissions and online/busy runner state prevent local removal. Verify failed/interrupted migration can be retried with a fresh token, and no PAT/token/credential content is persisted in `Data` or logs.
6. For complete coverage, run all 23 checks in `docs/TESTING.md`, including corrupt manifests, reparse paths, queue-enabled rejection and expiring tokens. Keep evidence and note what was not tested.

### Blocker B: publication

After Blocker A is approved, verify the exact commit and live GitHub CI. The initial release candidate with correct docs is `70229481c01d4de469f9007ecd9938fa0be92c48`; do not silently switch to an arbitrary HEAD.

In the NRS Workbench repository's PowerShell checkout (the user previously used `G:\NRS Workbench`):

```powershell
git fetch origin --tags
git show 70229481c01d4de469f9007ecd9938fa0be92c48:Directory.Build.props | Select-String '<Version>'
git ls-remote --tags origin refs/tags/v0.18.7
git tag -l v0.18.7
```

If the version is `0.18.7`, CI green, no tag exists, and Blocker A passed, run:

```powershell
git tag -a v0.18.7 70229481c01d4de469f9007ecd9938fa0be92c48 -m "NRS Workbench v0.18.7 Public Preview"
git push origin refs/tags/v0.18.7
```

The tag push invokes `.github/workflows/release.yml` on the Windows self-hosted runner. If it fails, diagnose first: **never move or delete an existing release tag**. The workflow also supports `workflow_dispatch` on `main` with input `tag=v0.18.7` to recover without moving the tag.

Consider v0.18.7 published **only after** Release workflow SUCCESS and a GitHub prerelease with `NRSWorkbench-v0.18.7-win-x64.zip` exists and can be downloaded.

After publication, update README, CHANGELOG release date, HANDOFF, ROADMAP and live GitHub Wiki. Confirm asset SHA256/size and test extracted startup. Never report background tasks as completed without verification.

### Priority backlog after 0.18.7

1. **v0.19.0 GitHub Actions Control**: design read-only workflow/runs overview first; then explicitly authorized workflow dispatch, rerun and cancel actions with least-privilege permissions, clear confirmation and audit. See `docs/ROADMAP.md`.
2. **Portable Workspace follow-up**: act on real manual migration bugs, safety corner cases and user-facing help, without adding service-runner migration unless separately scoped.
3. **Performance and soak validation**: measure cold launch, idle CPU/RAM, logs, refresh stability, queue behavior with many runners, and the impact of scanning large `_work` trees. Do not make unsupported speed claims.
4. **Docs/product polish**: verify live Wiki; refresh outdated screenshot (currently based on v0.18.4); keep install guidance and security/privacy explanations accurate.
5. **Longer-term items**: separate product website/technical SEO project only if explicitly approved; GitLab provider only if supported by user demand; no assumption of a Linux port.
6. **No active GitHub issues or PRs** were present when checked, but recheck rather than trusting this snapshot.

### Continuity instruction to the next AI

You are working **only** on NRS Workbench, repository `Nfreakz/nrs-workbench`. First reread this HANDOFF, `docs/RELEASING.md`, `docs/TESTING.md`, `docs/ROADMAP.md`, `CHANGELOG.md` and the live GitHub state. Never conflate this application's credentials, runners, versions, repo or deployment with other NeoRS apps. Respect the user's self-hosted Windows X64-only policy. Finish and verify v0.18.7 publication before embarking on v0.19.0. Do not infer manual migration testing or publication from a successful CI run.
