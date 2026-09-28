# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-09-28

### Published baseline

- Latest published release before the current release preparation: `v0.18.3 Public Preview`.
- v0.18.4 release candidate is on `main` at `9ea411d7f161ae51222731c77bf6d4d7fc21f89a`.
- Post-merge CI for the v0.18.4 candidate completed successfully.
- Manual Windows validation of Repositories, Settings and compact runner details passed.
- GitHub Release notes use curated CHANGELOG sections instead of an automatic list of every PR.
- `main` is protected by repository rules requiring pull requests and blocking deletion/force-push.

### main

- Current v0.18.4 release candidate on `main`: `9ea411d7f161ae51222731c77bf6d4d7fc21f89a`.
- Release documentation gate changes are isolated in `docs/v0.18.4-release-doc-gate`.
- Professional graphite UI is the approved visual baseline.
- `docs/VISUAL_SYSTEM.md` remains the visual source of truth.

## v0.18.4 release candidate

The implementation and final polish are merged to `main`.

- PR #18 and PR #22 are merged.
- Version metadata: `0.18.4`.
- Manual Windows validation passed.
- CI on the final implementation commit passed.
- CHANGELOG/README release wording is prepared. The documentation gate now also requires the writing standard and Wiki review before tagging `v0.18.4`.

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

1. Merge PR #24 after its green CI.
2. Review/update the GitHub Wiki for v0.18.4. Do not mark this complete without reading the live pages.
3. Verify final `main` CI and the release documentation against `docs/WRITING_STYLE.md`.
4. Create tag `v0.18.4` only after explicit approval.
5. Verify the Release workflow creates the GitHub Release and `NRSWorkbench-v0.18.4-win-x64.zip`.
6. Verify the published notes, asset checksum and README links before calling the release complete.

## Future roadmap

- v0.18.5: Feedback & Diagnostics.
- v0.19.0: GitHub Actions Control.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.
