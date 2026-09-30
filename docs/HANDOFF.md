# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-09-30

- Latest published release: [v0.18.4 Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.4), built from tag `v0.18.4` at `e8c786e689d7ebbfb53f17d61e704e9a2a73aff9`.
- Published Windows asset: `NRSWorkbench-v0.18.4-win-x64.zip`. GitHub reports SHA256 `8f1f427dec4a04becc393faecf11a37ea31c98c67c6cc33d40cb16498635cc1a`.
- The tag predates later release automation and image repairs on `main`. Those later commits are not part of the tagged source or ZIP.
- Manual Windows validation of Repositories, Settings and compact runner details passed before tagging.
- `main` is protected by repository rules requiring pull requests and blocking deletion/force-push.
- PR #25 (runner discovery and Smart Queue selection) is separate feature work and is not part of v0.18.4.
- `docs/VISUAL_SYSTEM.md` remains the visual source of truth.

## v0.18.4 scope

- Contextual safe-action guidance for the selected repository.
- Session-only in-memory history for Fetch, Pull, Push, Commit and local branch switches.
- Visible AHEAD/BEHIND and local-change summary.
- Repository state re-inspected immediately before Pull/Push; stale state blocks write actions.
- Git inspection timeout.
- Compact runner-details layout fix.
- Optional Buy Me a Coffee link in Settings, opened only on click.

## Release documentation

README and the GitHub Wiki describe the published ZIP. The preview image uses fictional runner, repository, path and log data. Before the next version, read `docs/WRITING_STYLE.md`, review affected documentation and the real GitHub Wiki, and distinguish released features from work on `main`.

## PR #25 · work in progress

Branch: `feat/v0.18.5-runner-pool-discovery`.

Current scope:
- runner discovery validates real runner installation markers instead of relying on the folder name;
- automatic discovery remains shallow and can find common parent folders one level below a drive root;
- Settings can use all detected runners or an explicit Smart Queue pool;
- runners outside the selected pool are not started, stopped, rotated or counted against the Smart Queue limit;
- runner rows, context menus and Details/Log layout have received a compact graphite UI pass;
- portable settings and smoke tests cover the queue-pool selection.

This work remains unreleased and is not part of the tagged v0.18.4 ZIP. Reconcile and validate PR #25 against current `main` before any merge or version decision.

## Future roadmap

- v0.18.5: Runner control, discovery, Feedback & Diagnostics.
- v0.19.0: GitHub Actions Control.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.
