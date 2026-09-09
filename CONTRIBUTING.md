# Contributing

NRS Workbench is a source-available project licensed for noncommercial use under PolyForm Noncommercial 1.0.0.

## External code contributions

Bug reports, feature proposals and testing feedback are welcome. **Do not submit code pull requests yet.** NRS Workbench reserves separate commercial licensing rights, so external code contributions will only be accepted after the project publishes an explicit contributor agreement that preserves the ability to offer both the public noncommercial license and separate commercial licenses.

This avoids accidentally creating a copyright/licensing situation where a future commercial license could not cover community-contributed code.

Principles:

- Keep runner state detection conservative. Use `Unknown` rather than inventing certainty.
- Do not introduce credential reads for convenience.
- Prefer BCL / Windows APIs over unnecessary dependencies.
- Keep domain logic independent from WPF.
- Add user-facing errors that explain what failed and preserve detailed diagnostics in the application log.


## Validation for maintainers and future code contributions

Run on Windows:

```powershell
dotnet build .\NRSWorkbench.sln -c Release
dotnet run --project .\tests\NRS.Workbench.SmokeTests\NRS.Workbench.SmokeTests.csproj -c Release
.\scripts\public-audit.ps1
```

Changes to Git write operations should include a regression case in the smoke-test executable or a clear explanation of why the behavior cannot be automated safely. Never attach unsanitized runner credentials, private keys, tokens or private repository remotes to issues or pull requests.
