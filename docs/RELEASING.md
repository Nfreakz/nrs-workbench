# Releasing

NRS Workbench publishes pre-release builds before a stable `v1.0.0`.

## Before tagging

1. Build the solution in Release mode.
2. Run `tests/NRS.Workbench.SmokeTests`.
3. Run `scripts/public-audit.ps1`.
4. Confirm the version in `Directory.Build.props` and `CHANGELOG.md`.
5. Check that the README and Wiki clearly distinguish features in the release tag from features only on development `main`; do not advertise unreleased features as included in the downloadable ZIP.
6. Review `SECURITY.md` and `docs/PRIVACY.md` if any credential-adjacent behavior changed.
7. Confirm `LICENSE` still references `PolyForm-Noncommercial-1.0.0` and that commercial licensing language has not drifted.
8. Confirm the repository-scoped Windows x64 self-hosted runner is online. Hosted GitHub runners are not used by project policy.
9. Verify import preview, keep/replace/cancel choices and missing-path warnings on the destination PC.
10. Obtain explicit approval before merging to main, creating a tag or publishing a release.

## Published v0.18.1 and approved v0.18.2 promotion

v0.18.1 was built, tested and published on 2026-09-26. Its one-time CI promotion
workflow is retired. Never re-create or move the old tag.

v0.18.2 uses a separate one-time workflow, `.github/workflows/promote-approved-v0.18.2.yml`.
It may run only after a successful trusted push CI on the current `main` commit,
verifies that `Directory.Build.props` identifies exactly v0.18.2, refuses to move
an existing tag, and dispatches the normal tag-scoped Release workflow. Remove the
one-time promoter after the ZIP has been verified.

A version number, tag or successful CI run alone is **not** a published binary.
Confirm the `v0.18.2` GitHub release, successful Release workflow and
`NRSWorkbench-v0.18.2-win-x64.zip` asset before announcing availability.

## Tag release (only after explicit approval)

First merge the reviewed branch into `main`, verify HEAD and the approved version, and confirm that the tag does not already exist. Replace `<approved-version>` with the explicitly approved version. Never move or reuse an existing release tag.

```powershell
git switch main
git pull --ff-only origin main
git status --short
git log -1 --oneline
git ls-remote --tags origin refs/tags/v<approved-version>
# Stop if the tag already exists or HEAD is not the approved release commit.
git tag -a v<approved-version> -m "NRS Workbench v<approved-version> Public Preview"
git push origin v<approved-version>
```

The `Release` workflow runs on the repository-scoped local self-hosted Windows x64 runner. It builds the solution, executes the Git and portable-settings smoke tests, runs the public source audit, publishes the self-contained `win-x64` application, creates the ZIP and uploads it to a GitHub prerelease using the workflow token.

CI also produces and inspects a temporary candidate ZIP locally without uploading it; that check is not a GitHub release or a substitute for manual startup testing. The public ZIP is currently unsigned and can trigger Windows SmartScreen or Smart App Control; do not instruct users to disable system-wide security protections to install it.

The release step is designed to be idempotent: if the prerelease already exists for the tag, it reuses it; if the ZIP asset already exists, it does not upload a duplicate.
