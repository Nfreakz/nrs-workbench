# HANDOFF · NRS Workbench

## Application boundary

- Independent Neo RS desktop application for local Windows Git repositories and GitHub Actions self-hosted runners.
- Repository: https://github.com/Nfreakz/nrs-workbench
- Do not use this repository's paths, credentials, runner registrations, deployment decisions or release tags for any other Neo RS application.
- Default branch: `main`. Review its live HEAD, PRs, CI and Releases before modifying anything.

## Published baseline

- Last verified public download on 2026-09-26: `v0.18.1 Public Preview`, Windows x64 portable ZIP in GitHub Releases.
- `v0.18.1` adds the optional 1/2-runner automatic queue and restores queue-managed runners on disabling it; the list of managed stops is only in memory in that version.
- The `v0.18.1` tag and ZIP must remain immutable.

## v0.18.2 candidate

- Branch: `feat/v0.18.2-queue-safety-catalan`, based on verified `main` revision `372073062a56562e69a486606e02eef9a39e4b57`.
- Version metadata in `Directory.Build.props` is set to `0.18.2` for candidate builds; **no v0.18.2 tag, Release or published ZIP exists until verified**.
- Queue candidates are restricted to runners that were online on enable or subsequently became online; a stopped runner with no matching queue-owned stop entry remains manually stopped.
- Queue stop ownership is journaled at `%LOCALAPPDATA%\NRSWorkbench\runner-queue-state.json`, outside portable settings. Recovery after closing the app requires a per-session confirmation before any persisted runner is eligible for restarting. Declining leaves runners stopped and clears the journal.
- Catalan `ca` is available in the language selector. Resource `Localization/ca.json` covers all static `{local:Tr Source='...'}` keys; local and portable settings retain `ca`. Changing the language still requires an application restart.
- Test additions: manual-stop protection, owned rotation, recovery confirmation/decline, journal round-trip and Catalan settings/resources.
- `RUN_ME_FIRST.cmd` now delegates to a launcher that, when executed from a Git clone, requires a clean working tree and configured upstream, runs `git fetch --prune` plus `git pull --ff-only` on the current branch, and never changes branches or performs automatic reset/rebase/merge/stash/discard operations. Non-Git source archives are built as extracted.

## Verify before publication

1. Confirm the current branch and version and compare candidate diff with the latest `main`.
2. Run the repository-scoped trusted Windows CI, including .NET build, smoke tests, source audit and portable candidate packaging.
3. Check startup recovery prompt, disabling the queue after restart, busy-job protection and UI language selection manually on a Windows PC.
4. Ensure the README, CHANGELOG and Wiki distinguish current release binaries from development features.
5. Obtain explicit release approval before merging, tagging and publishing the next version, if not already granted in the current user request.
6. After successful release, verify the actual GitHub ZIP asset and its release workflow conclusion before claiming the version is downloadable.

## Other open work

- PR #8 updates only the public README presentation. Reconcile its release-status wording with the published ZIP before merging. Do not incorporate unrelated application changes into v0.18.2 by default.
