# Releasing

The public repository is intended to publish pre-release builds before a stable `v1.0.0`.

## Before tagging

1. Build the solution in Release mode.
2. Run `tests/NRS.Workbench.SmokeTests`.
3. Run `scripts/public-audit.ps1`.
4. Confirm the version in `Directory.Build.props` and `CHANGELOG.md`.
5. Review `SECURITY.md` and `docs/PRIVACY.md` if any credential-adjacent behavior changed.
6. Confirm `LICENSE` still references `PolyForm-Noncommercial-1.0.0` and that commercial licensing language has not drifted.

## Tag release

Push a semantic-version tag such as:

```powershell
git tag v0.15.1
git push origin v0.15.1
```

The `Release` workflow builds, smoke-tests, audits and publishes a self-contained `win-x64` ZIP using GitHub's own `gh` CLI.
