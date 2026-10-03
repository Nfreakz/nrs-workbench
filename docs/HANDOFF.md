# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Current verified state · 2026-10-01

- Latest published release: [v0.18.4 Public Preview](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.4), built from tag `v0.18.4` at `e8c786e689d7ebbfb53f17d61e704e9a2a73aff9`.
- Published Windows asset: `NRSWorkbench-v0.18.4-win-x64.zip`. GitHub reports SHA256 `8f1f427dec4a04becc393faecf11a37ea31c98c67c6cc33d40cb16498635cc1a`.
- The tag predates later release automation and image repairs on `main`. Those later commits are not part of the tagged source or ZIP.
- Manual Windows validation of Repositories, Settings and compact runner details passed before tagging.
- `main` is protected by repository rules requiring pull requests and blocking deletion/force-push.
- PR #25 (runner discovery and Smart Queue selection) is merged to `main`; it remains outside the tagged v0.18.4 release.
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

## v0.18.5 development state

Runner discovery / Smart Queue pool work was manually validated on Windows and merged through PR #25. Preview-worktree infrastructure was merged through PR #36. Current verified `main`: `c8250ddc39866591a3a73111c70af2ee44fdeeae` with post-merge CI #436 SUCCESS.

Current feature branch: `feat/v0.18.5-feedback-diagnostics`.

PR #35 is 0 commits behind current `main`. CI #437 passed on the reconciled implementation. Manual Windows validation is pending through `RUN_PREVIEW.cmd`; `qa-preview` points to the PR head used for that test.

Runtime-resilience follow-up after a real long-running-session failure:
- automatic runner/queue refresh is now single-flight; a slow cycle causes later timer ticks to be skipped instead of queued;
- periodic runner/resource faults are caught and rate-limited in the local log instead of surfacing a modal dialog every tick;
- the last-resort UI exception dialog is throttled to one report per minute during an error burst;
- unobserved task exceptions are recorded and marked observed;
- the tray menu is no longer rebuilt every few seconds when nothing changed; old WinForms menu items are disposed when a rebuild is actually required;
- the exact historical exception still requires the local pre-fix application log for confirmation.
- the uploaded pre-fix log for the 2026-10-03 incident contains no current managed exception after the 2026-10-01 startup, so a local runtime-session marker/heartbeat is added to detect future hard/unclean exits that bypass WPF exception handlers.

Feedback & Diagnostics development scope:
- open the repository's bug and feature-request templates from the app;
- generate an exact local preview before export;
- export only generated diagnostic text plus a README in a user-selected ZIP;
- sanitize configured paths, local user/machine identity, HTTP(S) user-info and GitHub token-shaped strings;
- never upload diagnostics automatically;
- improve the unexpected-UI-error path so the user can review/export diagnostics.

Current scope:
- runner discovery validates real runner installation markers instead of relying on the folder name;
- automatic discovery remains shallow and can find common parent folders one level below a drive root;
- Settings can use all detected runners or an explicit Smart Queue pool;
- runners outside the selected pool are not started, stopped, rotated or counted against the Smart Queue limit;
- runner rows, context menus and Details/Log layout have received a compact graphite UI pass;
- portable settings and smoke tests cover the queue-pool selection.

This v0.18.5 work remains unreleased and is not part of the tagged v0.18.4 ZIP. Do not publish or bump the version automatically.

## Future roadmap

- v0.18.5: Runner control, discovery, Feedback & Diagnostics.
- v0.19.0: GitHub Actions Control.
- Product website/SEO remains a separate web project or explicitly approved repository.
- GitLab support only if real demand appears.


## Preview testing workflow

The normal checkout remains on `main` and is the day-to-day NRS Workbench copy.

- `RUN_ME_FIRST.cmd` updates, builds and runs the normal checkout.
- `RUN_PREVIEW.cmd` never switches the normal checkout. It fetches the disposable remote `qa-preview` channel into a sibling Git worktree named `<repo>-preview`, builds it and launches only that copy.
- The launchers close an existing NRS Workbench process first. Normal and preview are intentionally one-or-the-other, not concurrent.
- Preview uses `%LOCALAPPDATA%\NRSWorkbenchPreview`. Before launch, its `settings.json` is refreshed from the normal profile so real runner/repository registrations can be tested without persisting preview changes into the normal profile. The queue recovery journal is not copied.
- `qa-preview` is only a movable test pointer. It is not a release branch and never replaces the feature branch/PR as source of truth.
