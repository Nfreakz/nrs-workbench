# Releasing

NRS Workbench publishes pre-release builds before a stable `v1.0.0`.

## Before tagging

1. Build the solution in Release mode.
2. Run `tests/NRS.Workbench.SmokeTests`.
3. Run `scripts/public-audit.ps1`.
4. Confirm the version in `Directory.Build.props` and `CHANGELOG.md`.
5. Review `SECURITY.md` and `docs/PRIVACY.md` if any credential-adjacent behavior changed.
6. Confirm `LICENSE` still references `PolyForm-Noncommercial-1.0.0` and that commercial licensing language has not drifted.
7. Confirm the repository-scoped Windows x64 self-hosted runner is online. Hosted GitHub runners are not used by project policy.

## Tag release

Update local `main` first, then create and push the semantic-version tag:

```powershell
git switch main
git pull --ff-only origin main
git tag -a v0.15.2 -m "NRS Workbench v0.15.2 Public Preview"
git push origin v0.15.2
```

The `Release` workflow runs on the repository-scoped local self-hosted Windows x64 runner. It builds the solution, executes the 22 Git safety smoke tests, runs the public source audit, publishes the self-contained `win-x64` application, creates the ZIP and uploads it to a GitHub prerelease using the workflow token.

The release step is designed to be idempotent: if the prerelease already exists for the tag, it reuses it; if the ZIP asset already exists, it does not upload a duplicate.
