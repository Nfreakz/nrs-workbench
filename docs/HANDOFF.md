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

- Current base for v0.18.4: `bf66a4cba535cffd156f5760b88d9c8ac89a9e70`.
- Professional graphite UI is the approved visual baseline.
- `docs/VISUAL_SYSTEM.md` remains the visual source of truth.

## v0.18.4 candidate

Active work is isolated in PR #18.

- Branch: `feat/v0.18.4-repository-guidance-v2`.
- Version metadata: `0.18.4`.
- Rebuilt on top of the current post-v0.18.3 `main`.
- Do not tag or publish until manual Windows validation and CI are complete.

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

1. Let CI validate the rebuilt PR #18 candidate.
2. Test the v0.18.4 branch on Windows.
3. Merge only after UI and Git-safety behavior are confirmed.
4. Publish only after explicit approval.

## Future roadmap

- v0.18.5: Feedback & Diagnostics.
- v0.19.0: GitHub Actions Control.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.
