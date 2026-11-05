# GitHub Wiki update checklist · v0.18.7

**This repository file is the prepared source for a Wiki update, not evidence that the live Wiki was edited.** The connected GitHub interface does not provide Wiki read/write actions. After the release ZIP is verified, an authorized GitHub Wiki editor must compare the live pages and apply the updates below.

## Wiki Home

Set latest Windows Public Preview to [NRS Workbench v0.18.7](https://github.com/Nfreakz/nrs-workbench/releases/tag/v0.18.7) and link the [Windows x64 ZIP](https://github.com/Nfreakz/nrs-workbench/releases/download/v0.18.7/NRSWorkbench-v0.18.7-win-x64.zip).

A concise introduction can use:

> NRS Workbench v0.18.7 adds optional Portable Workspace settings stored under a `Data` folder next to the executable. Same-volume runner and repository paths can survive removable drive-letter changes. It also includes the v0.18.6 Maintenance Center, Runner Doctor, Feedback & Diagnostics, and Smart Queue.
>
> Portable runner re-registration across different physical PCs has not yet been independently validated. Test only with disposable stopped interactive GitHub.com runners; service-installed runners are not supported. No migration is started automatically when a drive is inserted.

Keep current system requirements: Windows 10/11 x64 and the self-contained ZIP. Clarify that extraction alone is not the same as enabling portable settings.

## New or revised Portable Workspace page

Use the checked-in [Portable Workspace guide](PORTABLE_WORKSPACE.md) as the content basis. Make clear that:

- Enabling portable mode creates `NRSWorkbench.portable` and stores app data under `Data` beside the executable.
- Same-volume paths are recorded using `@portable/...`; changing drive letter is intended to preserve those references.
- A runner's GitHub registration is separate from the Workbench portable settings and can require explicit preparation on another PC.
- Only stopped interactive GitHub.com runners are supported. Service-installed, running and busy runners are excluded.
- Smart Queue must be disabled before preparing runners.
- The PAT is supplied for a specific preparation session, not deliberately stored. Never post PATs or runner credentials with issues.
- Existing registrations are not modified merely by inserting a drive. Interrupted migrations must be retried explicitly.
- Second-PC preparation remains a Public Preview feature awaiting an independently witnessed manual trial using a disposable runner.

## Maintenance Center

Carry forward the v0.18.6 documentation:

- Workbench and PREVIEW logs are low-risk candidates for reviewed cleanup.
- Old runner `_diag` cleanup is optional and reduces local history.
- A runner that has become busy is not cleaned; newest Runner and Worker diagnostic logs are preserved.
- The `_work` folder is inventory-only and never automatically deleted.
- Large scans display partial results and mark incomplete measurements.
- A longer detail row and explicit deletion labels explain whether an item is low risk, history-affecting or protected.

## Privacy and release caveats

- Never tell users to disable system-wide Windows security features for an unsigned ZIP.
- Keep the Public Preview designation. Do not claim a successful physical migration that has not been tested.
- Cross-reference [Privacy](PRIVACY.md) and the repository [Security Policy](../SECURITY.md).

## Final Wiki checklist

- [ ] Open and review the live Home page and any existing Portable Workspace or Maintenance pages.
- [ ] Replace stale v0.18.5/v0.18.6 latest-version links with v0.18.7, without rewriting historical version pages.
- [ ] Add/revise portable-mode guide with the safety notice above.
- [ ] Verify live Wiki links and release ZIP are accessible.
- [ ] Record completion in `docs/HANDOFF.md`. Until then, mark the Wiki update **pending**, not completed.
