using System.ComponentModel;
using System.Globalization;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Auth;

public sealed class AuthSetCommand(IAnsiConsole console, IPresetStore store) : Command<AuthSetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<COOKIE>")]
        [Description("Session cookie for the availability API as \"name=value\" (copy it from your browser's DevTools).")]
        public string Cookie { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cookie = settings.Cookie.Trim();
        if (!AuthCookie.IsValidPair(cookie))
        {
            console.MarkupLine("[red]Error:[/] Expected a cookie pair in the form \"name=value\".");
            return 1;
        }

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

        config.AuthCookie = cookie;
        store.Save(config);
        console.MarkupLineInterpolated($"[green]Auth cookie '{AuthCookie.Name(cookie)}' saved.[/]");

        if (AuthCookie.TryDecodeMetadata(cookie) is { ExpiresAt: { } expiresAt })
        {
            console.MarkupLineInterpolated(expiresAt <= DateTimeOffset.UtcNow
                ? (FormattableString)$"[yellow]Warning:[/] This cookie expired at {expiresAt.ToString("u", CultureInfo.InvariantCulture)}."
                : $"[grey]Expires at {expiresAt.ToString("u", CultureInfo.InvariantCulture)}.[/]");
        }

        return 0;
    }
}
