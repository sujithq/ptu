---
name: ptu-availability-direct
description: >-
  Independently checks Azure PTU and PAYG Standard availability without running
  the installed `ptu` tool. Use when the user wants an independent availability
  lookup, asks to verify a ptu CLI result, or requests the same default report
  through the underlying data sources. Uses the active preset or factory
  defaults recorded by the ptu project.
user-invocable: true
tools: ["read", "search", "runCommands"]
---

# PTU Availability (Direct Sources)

Retrieve the same PTU and PAYG Standard availability data as `ptu availability`
but query the configured API and Microsoft Learn directly. This agent exists
for independent verification and must not use the installed `ptu` executable
to obtain, cross-check, or reproduce the requested availability information.
Do not run `ptu`, `dotnet tool run ptu`, or another wrapper around the installed
tool.

## Resolve the same defaults

1. Read the user's PTU configuration without printing or exposing secrets.
   On Windows, check `%APPDATA%\ptu\config.json`; on Linux/macOS, check
   `~/.config/ptu/config.json`.
2. Use `defaultPreset` and that preset's `regions`, `models`, `types`, and
   `tab`. A user-specified preset or explicit filters override those values.
   If no configuration exists, inspect this workspace's
   `src/Ptu.Cli/Configuration/PtuDefaults.cs` and use its factory preset.
3. If the config file has no usable API endpoint, report that a direct PTU
   query cannot be made. Do not guess an endpoint or substitute another
   source for private PTU capacity.

## Query PTU directly

Inspect `src/Ptu.Cli/Availability/HttpAvailabilityClient.cs` for the current
endpoint request and response contract; do not use the installed executable.
Send a GET request to the configured `apiEndpoint`, using the configured
`authCookie` as the `Cookie` header when present, and the browser-like
User-Agent, Accept, and Referer headers documented by that client. Do not log,
echo, or include the cookie in the answer or command output. Do not modify the
configuration. If authorization fails or the endpoint is unavailable, state
the failure clearly.

The API response contains status, generation time, and
`payload.regions[].models[]`. Match region/model names case-insensitively and
read each selected type's support and capacity fields:

- Data Zone: `dataZoneProvisionedAvailable`, `dataZoneProvisionedCapacity`
- Regional: `provisionedAvailable`, `provisionedCapacity`
- Global: `globalProvisionedAvailable`, `globalProvisionedCapacity`

Only treat a response with a successful status as current data. If the API
returns a failed status with a usable cached dataset and generation time,
identify the data as cached; otherwise report the failure, not an invented
availability result.

## Query PAYG Standard directly

Fetch Microsoft's public
[Foundry Models region availability](https://learn.microsoft.com/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability?pivots=standard&tabs=az-europe)
page for the selected `tab` (default to the preset's tab, or the project's
factory default). Read the Global Standard, Data Zone Standard, and
Standard/Regional tables, matching model and region case-insensitively.
Aggregate model versions: PAYG is `yes` when at least one documented version
has an availability checkmark for the region, and `no` when none does. If
the page or required table cannot be retrieved or parsed, report `unknown`;
never convert retrieval/parsing failure to `no`.

## Normalize and report

Use the same status semantics as the CLI:

- PTU `not tracked`: the model/region is absent from the PTU API response.
- PTU `not supported`: the record exists and its selected availability field
  is explicitly false.
- PTU `no capacity`: support is true and capacity is zero.
- PTU `yes`: support is true and capacity is positive.
- PTU `unknown`: support or capacity data is missing or unusable.
- PAYG Standard `yes`/`no`/`unknown`: use the Learn availability and
  retrieval rules above.

Return a concise table with model, region, selected deployment type, PTU
status/capacity, and PAYG Standard status. Note the preset or factory defaults
used and the PTU generation timestamp. Keep PTU and PAYG results clearly
separated and identify any source errors; do not claim this direct result was
produced by the installed CLI.
