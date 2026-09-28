# NRS Workbench writing style

This file is the writing source of truth for public NRS Workbench documentation and release text.

## Core rule

Write like technical product documentation, not marketing copy and not generic AI prose.

- Prefer concrete facts over claims such as "powerful", "seamless", "smart" or "professional".
- Say what changed, where it changed and what the user can do now.
- Do not pad short changes into three-part slogans or repeated conclusions.
- Avoid promotional filler, exaggerated benefits and vague adjectives.
- Do not repeat the same point in README, release notes and changelog with slightly different wording unless each document needs it.
- Keep sentences short when the information is operational.
- Use product terms consistently: runner, repository, Pull, Push, Fetch, Commit, Smart Queue, Public Preview.
- Distinguish clearly between a published release, a release candidate and development work on `main`.
- Never describe an unreleased feature as available in the downloadable ZIP.

## Release notes

Use user-facing sections such as `Added`, `Improved` and `Fixed`.

Include:
- visible features;
- behavior changes that affect normal use;
- important reliability or safety fixes.

Exclude:
- PR numbers;
- internal branch names;
- CI implementation details;
- refactors with no user-visible effect;
- repetitive implementation notes.

## README and Wiki

README explains the current downloadable product. Wiki explains setup, operation and feature behavior.

Before each version:
1. review both against the release tag;
2. remove stale version references;
3. update screenshots or examples only when they no longer represent the current UI or behavior;
4. check that links point to the published version, not to a future tag;
5. keep technical caveats visible when they affect real use.

## Tone

Restrained, factual and technical. NRS Workbench is a developer utility. Documentation should read comfortably beside GitHub, .NET and Windows technical documentation.
