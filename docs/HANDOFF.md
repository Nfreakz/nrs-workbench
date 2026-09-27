# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-09-27

### Published baseline

- Latest published release: `v0.18.3 Public Preview`.
- Release commit: `f830eb57a515e5c16755514ae35b8b0f0f5dfbc4`.
- Verified asset: `NRSWorkbench-v0.18.3-win-x64.zip`.
- Release workflow completed successfully.
- GitHub Release notes use curated CHANGELOG sections instead of an automatic list of every PR.
- `main` is protected by repository rules requiring pull requests and blocking deletion/force-push.

### main

- Current v0.18.4 integration commit on `main`: `77a4a633b4b90af1067f9155d823dda31304aa9e`.
- Post-merge CI for that commit completed successfully.
- Professional graphite UI is the approved visual baseline.
- `docs/VISUAL_SYSTEM.md` remains the visual source of truth.

## v0.18.4 candidate

The core v0.18.4 scope has been merged to `main`; final UI/documentation polish is isolated in `polish/v0.18.4-final`.

- PR #18 is merged.
- Version metadata: `0.18.4`.
- Manual Windows validation of the core Repositories and Settings changes has passed.
- Final polish adds clearer recommendation hierarchy and an empty state for session activity.
- Do not tag or publish until the polish PR is validated and merged.

Scope:
- contextual safe-action guidance for the selected repository;
- session-only in-memory history for Fetch, Pull, Push, Commit and local branch switches;
- visible BEHIND summary and correct dirty-repository counting;
- repository state re-inspected immediately before Pull/Push;
- stale branch, working-tree or local-commit state blocks write actions;
- no automatic merge, rebase, reset, stash, remote creation or branch creation.

## Validation for v0.18.4

1. Build and smoke tests must pass on the trusted Windows runner.
2. Manually check CLEAN, CHANGES, AHEAD, BEHIND, DIVERGED/CONFLICT and no-remote guidance.
3. Verify Fetch, Pull, Push, Commit and branch-switch history is session-only.
4. Verify Pull is blocked if the working tree changes after the displayed snapshot.
5. Verify Push is blocked if the branch changes after the displayed snapshot.
6. Check Repositories layout at normal Windows scaling before merge.

## Next sequence

1. Validate the `polish/v0.18.4-final` branch in CI.
2. Manually confirm the recommendation hierarchy and empty history state on Windows.
3. Merge the polish PR if clean.
4. Finalize CHANGELOG/README release wording.
5. Publish only after explicit approval.

## Future roadmap

- v0.18.5: Feedback & Diagnostics.
- v0.19.0: GitHub Actions Control.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.
