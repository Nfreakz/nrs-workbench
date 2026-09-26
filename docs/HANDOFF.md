# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-09-26

### main

- HEAD: `721b23a08e78ce48c8973d6e2b061dbbf56e8379`
- Commit: `style: professionalize NRS Workbench visual system before v0.18.3`
- Version metadata:
  - `Version 0.18.3`
  - `AssemblyVersion 0.18.3.0`
  - `FileVersion 0.18.3.0`
- v0.18.3 is integrated on `main` but is not yet a public GitHub Release.

### CI

Verified main workflow run: `36261738887`

Result: **SUCCESS**

Passed:
- Restore
- Build
- Git safety smoke tests
- Public source audit
- Portable distribution smoke

The portable distribution step intentionally validates packaging without uploading an artifact.

### Published baseline

- Latest verified public release: `v0.18.2 Public Preview`.
- Verified asset: `NRSWorkbench-v0.18.2-win-x64.zip`.
- Existing published release tags/assets are immutable; never move or replace them.

## v0.18.3 candidate

Integrated on `main`.

- Visible smart-queue dashboard with BUSY runners, next runner, queue-owned waiting runners and manually stopped runners.
- Session-only Pause/Resume control. Pause freezes queue scheduling only; it does not stop active jobs.
- Resource guard enabled by default.
- Default thresholds: CPU 85%, RAM 90%.
- New starts and idle rotations wait while CPU/RAM exceed the configured limits.
- Existing BUSY jobs are never interrupted by the guard.
- A 5-point recovery margin prevents rapid threshold flapping.
- Portable settings preserve the guard and thresholds.
- Professional graphite UI baseline is integrated and visually approved.
- `docs/VISUAL_SYSTEM.md` is the source of truth for future UI work.

## v0.18.4 candidate

Current implementation is isolated in PR #18.

- PR: `#18`
- Title: `feat: v0.18.4 repository guidance on professional UI baseline`
- Branch: `feat/v0.18.4-repository-guidance-v2`
- Head: `ab9af31f5b6ee44703c4eba5f540cd33aef87091`
- Base: `main` @ `721b23a08e78ce48c8973d6e2b061dbbf56e8379`
- State: open, draft, mergeable, not merged.
- CI run `36263398048`: **SUCCESS**
- PR #16 is obsolete, closed and not merged.

Scope:
- contextual safe-action guidance for the selected repository;
- session-only in-memory history for Fetch, Pull, Push, Commit and local branch switches;
- BEHIND visibility in repository summary;
- dirty repositories counted even when their primary state is AHEAD/BEHIND;
- repository state re-inspected immediately before Pull/Push;
- stale branch, working-tree or local-commit state blocks the write action;
- no automatic merge/rebase/reset/stash/remote creation/branch creation.

Do not merge PR #18 until the v0.18.3 release decision is complete.

## Release gate for v0.18.3

Before publishing:

1. Confirm `main` still points to the intended v0.18.3 commit and version metadata.
2. Confirm trusted Windows CI remains green.
3. Manually check on Windows:
   - startup recovery prompt;
   - disabling the queue after restart;
   - BUSY-job protection;
   - language selection;
   - Smart Queue resource hold/recovery behavior;
   - the approved graphite UI at normal Windows scaling.
4. Ensure README/CHANGELOG/Wiki still distinguish the published v0.18.2 binary from unreleased v0.18.3 features.
5. Obtain explicit user approval before creating any v0.18.3 tag or GitHub Release.
6. After release, verify the actual Windows x64 ZIP and the release workflow before claiming the version is downloadable.

## Next sequence

If v0.18.3 is explicitly approved for release:

1. re-verify `main` and CI;
2. prepare release notes;
3. create/tag/publish `v0.18.3`;
4. verify `NRSWorkbench-v0.18.3-win-x64.zip`;
5. then reconcile and merge PR #18;
6. only after that proceed to v0.18.5.

If release approval is not given yet:

- keep PR #18 separate;
- continue v0.18.3 validation only;
- do not tag or publish.

## Future roadmap

- v0.18.5: Feedback & Diagnostics.
- v0.19.0: GitHub Actions Control.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.
- Measure startup time, process RAM, idle CPU and refresh impact before making public lightweight-performance claims.
