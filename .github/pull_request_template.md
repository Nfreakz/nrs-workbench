## What changed

Describe the behavior changed by this PR.

## Validation

- [ ] `dotnet build .\NRSWorkbench.sln -c Release`
- [ ] `dotnet run --project .\tests\NRS.Workbench.SmokeTests\NRS.Workbench.SmokeTests.csproj -c Release`
- [ ] `.\scripts\public-audit.ps1`
- [ ] No credentials, local logs, private repository data or personal absolute paths added

## Safety

If this changes Git writes, runner lifecycle control, logs or credential-adjacent behavior, explain the safeguards and failure mode.
