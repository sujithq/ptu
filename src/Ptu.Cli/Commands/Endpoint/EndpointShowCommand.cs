using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Endpoint;

public sealed class EndpointShowCommand(IAnsiConsole console, IPresetStore store) : Command<EmptyCommandSettings>
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

        if (string.IsNullOrWhiteSpace(config.ApiEndpoint))
        {
            console.MarkupLine("Endpoint: [grey]not configured[/] - set it with 'ptu endpoint set <URL>' or run 'ptu availability' once interactively.");
        }
        else
        {
            console.MarkupLineInterpolated($"Endpoint: {config.ApiEndpoint}");
        }

        return 0;
    }
}
