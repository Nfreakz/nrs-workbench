# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-09-27

### Published baseline

- Latest published release before the current release preparation: `v0.18.3 Public Preview`.
- v0.18.4 release candidate is on `main` at `8ea7f8d1eb471f5360c09e341ec1205fe50c02c3`.
- Post-merge CI for the v0.18.4 candidate completed successfully.
- Manual Windows validation of Repositories, Settings and compact runner details passed.
- GitHub Release notes use curated CHANGELOG sections instead of an automatic list of every PR.
- `main` is protected by repository rules requiring pull requests and blocking deletion/force-push.

### main

- Current v0.18.4 release candidate on `main`: `8ea7f8d1eb471f5360c09e341ec1205fe50c02c3`.
- Release-prep changes are isolated in `release/v0.18.4`.
- Professional graphite UI is the approved visual baseline.
- `docs/VISUAL_SYSTEM.md` remains the visual source of truth.

## v0.18.4 release candidate

The implementation and final polish are merged to `main`.

- PR #18 and PR #22 are merged.
- Version metadata: `0.18.4`.
- Manual Windows validation passed.
- CI on the final implementation commit passed.
- Release preparation finalizes CHANGELOG/README wording before tagging `v0.18.4`.

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

1. Validate and merge `release/v0.18.4`.
2. Tag the final release-prep commit as `v0.18.4`.
3. Verify the release workflow creates the GitHub Release and Windows x64 ZIP.
4. Verify release notes and asset checksum before calling the release complete.

## Future roadmap

- v0.18.5: Feedback & Diagnostics.
- v0.19.0: GitHub Actions Control.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.
