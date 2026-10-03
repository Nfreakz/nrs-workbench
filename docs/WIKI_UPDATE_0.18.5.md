# GitHub Wiki update checklist · v0.18.5

This file is a release-preparation aid for the live GitHub Wiki. It is not the Wiki itself.

The current tool connection does not expose GitHub Wiki read/write operations, so the live Wiki must not be claimed as updated until it is reviewed in a Wiki-capable GitHub session.

## Home / current release

After v0.18.5 is published:
- set the current Public Preview to **v0.18.5**;
- link to the v0.18.5 release and `NRSWorkbench-v0.18.5-win-x64.zip`;
- summarize Runner Doctor, Feedback & Diagnostics, explicit Smart Queue runner pools and long-session reliability changes;
- keep Windows 10/11 and portable x64 requirements visible.

## Runner management

Document that runner discovery uses real installation markers rather than folder-name prefixes.

Document the Smart Queue pool:
- use all detected runners, or choose an explicit subset;
- excluded runners are not started, stopped, rotated or counted against the queue limit;
- active BUSY jobs remain protected.

## Runner Doctor

Add a Runner Doctor section:
- OK / Attention / Problem is based on explainable local checks;
- checks cover installation markers, local registration marker, runner state, listener/Worker processes, Windows service state, version and local `_diag` activity;
- STOPPED is not automatically an error;
- Runner Doctor is read-only and does not repair, re-register or update runner binaries;
- it does not read runner credential files.

## Feedback & Diagnostics

Document:
- Report an Issue and Suggest Feature open GitHub templates;
- diagnostics are previewed before export;
- nothing is uploaded automatically;
- exported ZIP contains generated diagnostic text and a README only;
- configured paths, local identity and token-shaped values are redacted on a best-effort basis;
- the preview is the final privacy check before sharing.

## Reliability / troubleshooting

Document:
- automatic runner/queue refresh cycles do not overlap;
- recurring refresh faults are contained and repeated last-resort UI dialogs are throttled;
- a local runtime-session heartbeat helps identify an unclean/hard exit on the next launch;
- the heartbeat does not contain repository contents or runner credentials.

## Screenshot note

The repository README still uses the representative v0.18.4 main-dashboard image because no verified v0.18.5 screenshot was available during release preparation. Do not relabel that image as a v0.18.5 capture.
