# Portable Workspace · NRS Workbench v0.18.7

This guide applies only to NRS Workbench in `Nfreakz/nrs-workbench` and its Windows x64 Public Preview. It does not apply to other NeoRS apps or their runners.

> **Before moving runners:** keep a backup of existing registrations, stop the runner, and test the procedure with a disposable interactive GitHub.com runner. Cross-PC runner re-registration is still awaiting a full independent physical test.

## What portable means

The ZIP contains a self-contained Windows application, but **extracting the ZIP alone does not enable a portable profile**. Portable Workspace can be activated explicitly from the app. Activation creates the `NRSWorkbench.portable` marker beside `NRSWorkbench.exe` only after portable settings and manifest preparation succeeds.

With the marker present, application data is stored in `Data` next to the executable instead of the normal profile under `%LOCALAPPDATA%\NRSWorkbench`. Same-volume runner roots, repository paths, runner ordering and selected Smart Queue pool paths are recorded using `@portable/...` entries so changing the drive letter does not automatically break them.

Application data is not the same as a GitHub Actions runner registration. Runner credentials may be tied to the original machine, so moving a folder to another PC does **not** make the runner ready for use automatically.

## Safe preview check

1. Extract v0.18.7 to a disposable directory on a removable drive. Keep the current NRS Workbench copy and production runner directories untouched.
2. Launch the extracted `NRSWorkbench.exe` and use the Portable Workspace control in the interface to activate portable mode.
3. Restart this copy. Check that `NRSWorkbench.portable` exists beside the executable and that settings appear under its adjacent `Data` directory.
4. Before changing drives, turn off Smart Queue and stop any **disposable** interactive test runner. Never move an active runner while it is executing a job.
5. Change the external drive letter or move the copy to a second Windows test PC. Verify same-volume paths still resolve. On the second PC, a stopped runner that was registered on the first PC should be shown as **Needs preparation** rather than assumed ready.
6. For actual re-registration, use only a disposable interactive runner, with the GitHub permissions required for that runner's target. Enter the PAT in the app for that session; do not paste tokens into logs, tickets or chat.
7. After preparation, check that the runner name, GitHub target, work folder, labels and group have been preserved, then verify a test job can run. Review the adjacent `Data` files and local logs for credentials or tokens; do not publish their contents.

## Restrictions and safeguards

- Portable preparation is limited to **stopped interactive runners on GitHub.com**. Service-installed runners are not supported.
- A runner GitHub reports as online or busy must not be prepared. Smart Queue must be disabled.
- Unsupported targets, duplicate target/name identities, invalid or escaping work folders, symlinks/junctions and incomplete runner installations are blocked.
- The complete selection is checked before any local runner configuration is removed. GitHub state is checked again immediately before each runner's local replacement.
- A PAT is only used for the preparation session. The app requests short-lived registration tokens and does not intentionally persist the PAT or registration tokens.
- Incomplete migration can remain **pending** and needs a deliberate retry; simply inserting the disk never starts a migration.
- Do not copy or share `.credentials`, `.credentials_rsaparams`, access tokens or private keys.
- A corrupt portable manifest must fail closed instead of silently discarding pending state.

For detailed fault and security test cases, see [TESTING.md](TESTING.md). For supported features and future development, see [ROADMAP.md](ROADMAP.md).

## If something goes wrong

Do not manually delete a runner's registration files or reconnect production runners while diagnosing a failed trial. Keep the disposable copy and its non-secret diagnostic summary, review the reported state (especially **Migration pending / retry**) and file a [GitHub issue](https://github.com/Nfreakz/nrs-workbench/issues/new/choose) without attachments containing credentials.

For normal operation and non-portable profiles, use the [README](../README.md).
