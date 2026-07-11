# Contributing to ptu

Thanks for your interest in contributing!

## Prerequisites

- .NET SDK `11.0.100-preview.5` or later ([global.json](global.json) pins the exact version; `rollForward: latestFeature` accepts newer previews).
- The .NET 10 and 11 runtimes to run the multi-targeted test suite.

## Build and test

```pwsh
dotnet build --nologo
dotnet test --nologo          # runs the suite on net10.0 and net11.0
dotnet run --project src/Ptu.Cli -f net10.0 -- --help
```

Every change is expected to keep the test suite green on **both** target frameworks. New behavior needs new tests — the suite uses `CommandAppTester` against the real `Program.Configure` wiring, so tests exercise actual command registration.

## Conventional commits (required)

Commit messages drive versioning and changelogs via [Versionize](https://github.com/versionize/versionize):

| Prefix | Effect |
|---|---|
| `fix:` | patch release |
| `feat:` | minor release |
| `feat!:` or `BREAKING CHANGE:` footer | major release |
| `docs:`, `test:`, `refactor:`, `perf:`, `ci:`, `build:`, `chore:` | patch release, grouped in the changelog |

Never edit `<Version>` in the csproj by hand — Versionize owns it.

## Pull requests

1. Fork and create a topic branch from `main`.
2. Make your change with tests; keep commits conventional.
3. Ensure `dotnet test` passes and the CI workflow is green.
4. Open a PR describing the motivation and behavior change.

## Sensitive values

The availability API endpoint is user-supplied configuration. Never hardcode private endpoints, keys, or organization-internal URLs in code, docs, or tests — use placeholders like `https://your-availability-api.example.com/...`.

## Releasing (maintainers)

```pwsh
dotnet versionize                      # bump + CHANGELOG + chore(release) commit + tag
git push --follow-tags origin main    # tag triggers the release workflow (NuGet + GitHub release)
```
