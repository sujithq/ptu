---
name: ptu-availability
description: >-
  Looks up Azure PTU and PAYG Standard model availability by running the
  installed `ptu availability` command. Use when the user asks whether a model
  is available in a region, wants the default PTU availability report, or
  asks to compare Regional, Data Zone, or Global availability. Uses the
  installed tool's active preset and defaults when the user gives no filters.
user-invocable: true
tools: ["runCommands"]
---

# PTU Availability (Installed CLI)

Use the installed `ptu` tool as the source of the availability report. The
tool already knows the configured API endpoint, authentication cookie, active
preset, and PAYG geography. Do not query the endpoint or Microsoft Learn
separately for the same availability data.

## Workflow

1. If the user specifies regions, models, deployment types, a preset, or a
   geography, pass those options to `ptu availability`. Otherwise run
   `ptu availability` with no arguments so the active preset and installed
   tool defaults are used.
2. Use `--refresh` only when the user explicitly asks for fresh data.
3. Report the command result faithfully, including any warning, cached-data
   notice, or API failure. Never infer missing values or present a failed
   request as successful.
4. Summarize the returned regions, models, deployment types, and statuses.
   Preserve the distinction between PTU and PAYG Standard columns.

## Status interpretation

- PTU `yes`: support is reported and capacity is positive.
- PTU `no capacity`: support is reported, but capacity is zero.
- PTU `not supported`: the API explicitly reports no support for that type.
- PTU `unknown`: support or capacity data is missing or unusable.
- PTU `not tracked`: the API did not return the model/region combination.
- PAYG Standard `yes`: at least one documented model version is available.
- PAYG Standard `no`: none is listed as available for that region.
- PAYG Standard `unknown`: the Microsoft Learn data could not be retrieved
  or parsed.

PTU Regional, Data Zone, and Global results are independent. A model can have
different statuses for each type. Do not interpret a missing (`-`) capacity
as a status.

## Output

Give a concise table of the relevant result rows, preserving the CLI's status
labels and capacity values. State when the report used the active preset, and
include data timestamps or warnings printed by the tool. If the user asks for
the full output, show it without changing the statuses.
