# ptu

Compare Azure PTU (provisioned throughput) and PAYG Standard model availability per region, straight from your terminal. Data Zone PTU and PAYG by default — Regional and Global PTU/PAYG on demand — with results grouped by model.

## Install

```shell
dotnet tool install --global sujithq.ptu.cli
```

Requires the .NET 9, .NET 10, or .NET 11 runtime. .NET 9 is out of support; prefer .NET 10 or later when possible.

## First run

On first use, `ptu availability` asks for the availability API endpoint and stores it in your user configuration (`%APPDATA%\ptu\config.json` on Windows, `~/.config/ptu/config.json` on Linux/macOS).

## Check availability

```shell
ptu availability                                          # uses the active preset
ptu availability --refresh                                # bypasses caches and requests fresh data
ptu availability -r swedencentral,francecentral -m gpt-4.1
ptu availability -r uksouth -m gpt-5.6-luna -t regional
ptu availability --tab az-americas -r eastus -m gpt-4.1
ptu availability -t datazone,global                       # PTU and PAYG Standard types: datazone (default), regional, global
ptu availability -m gpt-4.1 -t datazone,global --show-quota
ptu availability -m gpt-4.1 -t datazone,global --show-quota --quota-layout single
ptu availability --preset eu
```

Regions and models are repeatable or comma-separated and matched case-insensitively; explicit flags override the preset.

If the latest API refresh failed but its response includes the last successful dataset, `ptu` renders that cached data with a warning and its generation timestamp. A failed response without cached regions remains an API failure (exit code `2`).

PTU status distinguishes `yes` (supported with positive capacity), `no capacity` (supported with zero capacity), and `not supported` (the API reports no support for that deployment type). Missing or unusable support/capacity data is `unknown`; a model/region absent from the API is `not tracked`. Regional, Data Zone, and Global support and capacity are independent.

PAYG Global Standard, Data Zone Standard, and Regional Standard availability is retrieved on each run from Microsoft's public [Foundry model region availability](https://learn.microsoft.com/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability?pivots=standard&tabs=az-europe#global-standard) page. `-t|--type` selects matching PTU and PAYG columns (`datazone` by default; also `regional` and `global`), and the selection can be saved with `ptu preset set <name> --types datazone,global`. The Learn geography defaults to Europe (`az-europe`) and can be selected with `--tab az-americas`, `--tab az-europe`, `--tab az-apac`, or `--tab az-mea`; an explicit flag overrides the preset. `yes` means at least one documented Azure OpenAI model version is available in that region. If Microsoft Learn is unavailable, PTU results still render and the PAYG columns show `unknown`.

Pass `--show-quota` to append the documented PAYG Standard RPM and TPM limits for the selected models and deployment types, grouped into a separate table for each quota tier by default. Use `--quota-layout single` to combine all tiers into one table with a Tier column. Values are shown exactly as published, including rate windows such as `300 / 10s` and `-` when no TPM limit is listed. Quota limits are scoped by subscription and deployment type (and by data zone for Data Zone Standard); they are not regional capacity values.

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
ptu preset set eu --regions francecentral --models gpt-4.1,gpt-5-mini --types datazone,global --tab az-europe
ptu preset use eu
ptu preset remove eu                                      # 'default' is protected
ptu preset reset [--all]
```

## Exit codes

`0` success · `1` invalid input · `2` API failure

## Links

Source, issues, and contributor docs: [github.com/sujithq/ptu](https://github.com/sujithq/ptu) (MIT license)
