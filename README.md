# ptu

A .NET 11 CLI base built with [Spectre.Console](https://spectreconsole.net), tested with xUnit, and wired for AI-assisted QA via GitHub Copilot agents and [APM](https://github.com/microsoft/apm)-managed skills.

## Layout

| Path | Purpose |
|---|---|
| [src/Ptu.Cli](src/Ptu.Cli) | CLI app (`Spectre.Console.Cli`). Commands live in `Commands/`, wiring in `Program.Configure`. |
| [tests/Ptu.Cli.Tests](tests/Ptu.Cli.Tests) | xUnit tests using `Spectre.Console.Cli.Testing`'s `CommandAppTester`. |
| [.github/agents](.github/agents) | Copilot custom agents: the repo-specific `qa` agent + test agents from `dotnet/skills`. |
| [.agents/skills](.agents/skills) | Agent skills installed by APM (run-tests, coverage-analysis, test-gap-analysis, ...). |
| [apm.yml](apm.yml) | APM manifest declaring skill dependencies (locked in `apm.lock.yaml`). |
| [global.json](global.json) | Pins the .NET 11 preview SDK. |

## Prerequisites

- To **install and run** the tool: .NET 10 (LTS) or .NET 11 runtime
- To **build** this repo: .NET SDK `11.0.100-preview.5` or later (the package multi-targets `net10.0;net11.0`)
- [APM CLI](https://github.com/microsoft/apm) (only needed to manage skills)

## Build, test, run

```pwsh
dotnet build
dotnet test
dotnet run --project src/Ptu.Cli -f net10.0 -- availability -r swedencentral -m gpt-4.1
```

## Install as a dotnet tool

From [NuGet](https://www.nuget.org/packages/sujithq.ptu.cli) (package `sujithq.ptu.cli`, command `ptu`):

```pwsh
dotnet tool install --global sujithq.ptu.cli
ptu --version
ptu availability
```

Update with `dotnet tool update --global sujithq.ptu.cli`; remove with `dotnet tool uninstall --global sujithq.ptu.cli`.

For a local build: `dotnet pack src/Ptu.Cli -c Release` then `dotnet tool install --global sujithq.ptu.cli --add-source ./artifacts`.

## PTU availability

`ptu availability` shows provisioned-throughput availability grouped by model (one group per model, one row per region). On the very first run it asks for the availability API endpoint and stores it in the config file; the endpoint returns the full dataset, so filtering happens client-side. Data Zone PTU is shown by default; `--type` adds `regional`/`global`.

```pwsh
ptu availability                                        # active preset (factory: swedencentral,francecentral × gpt-5.4,gpt-5.4-mini,gpt-5-mini,gpt-4.1)
ptu availability -r swedencentral,francecentral -m gpt-4.1
ptu availability --preset eu -t datazone,global
```

Exit codes: `0` success, `1` invalid input (unknown preset/type, empty region or model list), `2` API failure.

## Endpoint

The endpoint is stored in `%APPDATA%/ptu/config.json` and can be inspected or changed at any time:

```pwsh
ptu endpoint show
ptu endpoint set https://your-availability-api.example.com/api/availability/azure-ptu
```

## Presets

Named region/model profiles stored in `%APPDATA%/ptu/config.json` (alongside the `apiEndpoint`); one is the active default used by `availability`.

```pwsh
ptu preset list                                         # * marks the active preset
ptu preset show [name]
ptu preset set eu --regions francecentral --models gpt-4.1,gpt-5-mini
ptu preset use eu                                       # switch the active default
ptu preset remove eu                                    # 'default' is protected
ptu preset reset [--all]                                # restore factory values (--all drops other presets)
```

## Adding a command

1. Create `src/Ptu.Cli/Commands/MyCommand.cs` extending `Command<TSettings>`; take `IAnsiConsole` in the constructor (keeps it testable).
2. Register it in `Program.Configure`.
3. Add tests in `tests/Ptu.Cli.Tests` via `CommandAppTester` — it reuses `Program.Configure`, so tests exercise the real wiring.

## AI-assisted QA

- **`@qa` agent** ([.github/agents/qa.agent.md](.github/agents/qa.agent.md)): builds, tests, checks coverage and test quality, and reviews diffs. Only ever edits `tests/`; hands off to `test-quality-auditor` and `code-testing-generator` for deep audits and test generation.
- **Skills**: installed from [dotnet/skills](https://github.com/dotnet/skills) (plugins `dotnet`, `dotnet-test`, `dotnet11`) into `.agents/skills/`.

Manage skills with APM:

```pwsh
apm install          # restore from apm.yml / apm.lock.yaml
apm update           # refresh to latest upstream refs
apm install dotnet/skills/plugins/<plugin> --target copilot   # add another plugin
```
