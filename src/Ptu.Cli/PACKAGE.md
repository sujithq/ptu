# ptu

Compare Azure PTU (provisioned throughput) and PAYG model availability per region, straight from your terminal. Data Zone PTU and PAYG by default — Regional and Global PTU on demand — with results grouped by model.

## Install

```shell
dotnet tool install --global sujithq.ptu.cli
```

Requires the .NET 10 (LTS) or .NET 11 runtime.

## First run

On first use, `ptu availability` asks for the availability API endpoint and stores it in your user configuration (`%APPDATA%\ptu\config.json` on Windows, `~/.config/ptu/config.json` on Linux/macOS).

## Check availability

```shell
ptu availability                                          # uses the active preset
ptu availability --refresh                                # bypasses caches and requests fresh data
ptu availability -r swedencentral,francecentral -m gpt-4.1
ptu availability --tab az-americas -r eastus -m gpt-4.1
ptu availability -t datazone,global                       # types: datazone (default), regional, global
ptu availability --preset eu
```

Regions and models are repeatable or comma-separated and matched case-insensitively; explicit flags override the preset.

PAYG Data Zone Standard availability is retrieved on each run from Microsoft's public [Foundry model region availability](https://learn.microsoft.com/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability?pivots=standard&tabs=az-europe#data-zone-standard) page. The Learn geography defaults to Europe (`az-europe`) and can be selected with `--tab az-americas`, `--tab az-europe`, `--tab az-apac`, or `--tab az-mea`; an explicit flag overrides the preset. `yes` means at least one documented Azure OpenAI model version is available in that region. If Microsoft Learn is unavailable, PTU results still render and the PAYG column shows `unknown`.

## Manage the endpoint

```shell
ptu endpoint show
ptu endpoint set https://your-availability-api.example.com/api/availability/azure-ptu
```

## Authentication

If the API is secured, copy its session cookie from a signed-in browser session (DevTools → Application → Cookies) and store it as `name=value`; it is sent as a `Cookie` header on every request:

```shell
ptu auth set "session_cookie=eyJ0b2tlbiI6..."
ptu auth show                                             # status and expiry - never the value
ptu auth clear
```

## Manage presets

Named region/model/Learn-geography profiles; one is the active default used by `availability`.

```shell
ptu preset list                                           # * marks the active preset
ptu preset show [name]
ptu preset set eu --regions francecentral --models gpt-4.1,gpt-5-mini --tab az-europe
ptu preset use eu
ptu preset remove eu                                      # 'default' is protected
ptu preset reset [--all]
```

## Exit codes

`0` success · `1` invalid input · `2` API failure

## Links

Source, issues, and contributor docs: [github.com/sujithq/ptu](https://github.com/sujithq/ptu) (MIT license)
