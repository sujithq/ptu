using System.Globalization;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Auth;

public sealed class AuthShowCommand(IAnsiConsole console, IPresetStore store) : Command<EmptyCommandSettings>
{
    protected override int Execute(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken)
    {
        PresetConfig config;
        try
        {
            config = store.Load();
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(config.AuthCookie))
        {
            console.MarkupLine("Auth cookie: [grey]not configured[/] - set it with 'ptu auth set \"<name>=<value>\"'.");
            return 0;
        }

        // Never echo the cookie value; it is a credential.
        console.MarkupLineInterpolated($"Auth cookie: [green]configured[/] ('{AuthCookie.Name(config.AuthCookie)}')");

        if (AuthCookie.TryDecodeMetadata(config.AuthCookie) is { } metadata)
        {
            if (metadata.Username is { Length: > 0 } username)
            {
                console.MarkupLineInterpolated($"User:        {username}");
            }

            if (metadata.ExpiresAt is { } expiresAt)
            {
                var stamp = expiresAt.ToString("u", CultureInfo.InvariantCulture);
                console.MarkupLineInterpolated(expiresAt <= DateTimeOffset.UtcNow
                    ? (FormattableString)$"Expires:     [red]{stamp} (expired)[/]"
                    : $"Expires:     {stamp}");
            }
        }

        return 0;
    }
}
