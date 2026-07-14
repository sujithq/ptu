using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Auth;

public sealed class AuthClearCommand(IAnsiConsole console, IPresetStore store) : Command<EmptyCommandSettings>
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
            console.MarkupLine("No auth cookie is configured.");
            return 0;
        }

        config.AuthCookie = null;
        store.Save(config);
        console.MarkupLine("[green]Auth cookie removed.[/]");
        return 0;
    }
}
