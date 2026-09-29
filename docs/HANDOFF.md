# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-09-29

### Published baseline

- Latest verified published release: `v0.18.3 Public Preview`.
- `v0.18.4` tag exists, but the GitHub Release and ZIP are still pending. Release run #16 built, tested, audited and packaged the tagged source successfully, then failed because the publish step still invoked the helper from the tagged checkout instead of the separate `.release-tools` checkout. The current fix points the publish step explicitly at `.release-tools/scripts/get-release-notes.ps1` and its CHANGELOG without moving the tag.
- `main` at `e8c786e689d7ebbfb53f17d61e704e9a2a73aff9`; post-merge CI #323 completed successfully.
- Manual Windows validation of Repositories, Settings and compact runner details passed.
- GitHub Release notes use curated CHANGELOG sections instead of an automatic list of every PR.
- `main` is protected by repository rules requiring pull requests and blocking deletion/force-push.

### main

- v0.18.4 implementation and documentation gate are merged on `main`.
- PR #25 (runner discovery and Smart Queue selection) remains separate and must not be described as part of the tagged v0.18.4 build.
- Professional graphite UI is the approved visual baseline.
- `docs/VISUAL_SYSTEM.md` remains the visual source of truth.

## v0.18.4 release candidate

The implementation and final polish are merged to `main`.

- PR #18 and PR #22 are merged.
- Version metadata: `0.18.4`.
- Manual Windows validation passed.
- CI on the final implementation commit passed.
- The Wiki was reviewed and updated. README and Wiki distinguish the published v0.18.3 ZIP from the tagged v0.18.4 source.

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

1. Resolve PR #26 and verify its CI. Do not recreate or move `v0.18.4`.
2. Recover the Release workflow for the existing tag only after the release process is approved.
3. Verify GitHub Release, `NRSWorkbench-v0.18.4-win-x64.zip`, checksum and final README/Wiki links before announcing publication.
4. Keep PR #25's feature work outside the v0.18.4 release.

## Future roadmap

- v0.18.5: Feedback & Diagnostics.
- v0.19.0: GitHub Actions Control.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.
