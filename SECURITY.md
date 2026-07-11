# Security Policy

## Supported versions

Only the latest release published to [NuGet.org](https://www.nuget.org/packages/sujithq.ptu.cli) receives security fixes.

## Reporting a vulnerability

Please **do not** open a public issue for security problems.

Instead, use [GitHub private vulnerability reporting](https://github.com/sujithq/ptu/security/advisories/new): it notifies the maintainer privately and supports coordinated disclosure.

You can expect an initial response within a few days. Once a fix is available it ships as a new NuGet release, and the advisory is published after users have had a reasonable window to update.

## Scope notes

- `ptu` stores its configuration (including the user-supplied availability API endpoint) in plain text under `%APPDATA%\ptu\config.json` (or the XDG equivalent). Treat that file as sensitive if your endpoint is private.
- The CLI makes outbound HTTPS requests only to the endpoint you configure.
