using System.ComponentModel;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Endpoint;

public sealed class EndpointSetCommand(IAnsiConsole console, IPresetStore store) : Command<EndpointSetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<URL>")]
        [Description("Absolute http(s) URL of the availability API endpoint.")]
        public string Url { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!CommandInput.IsValidHttpUrl(settings.Url))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] '{settings.Url}' is not an absolute http(s) URL.");
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

        config.ApiEndpoint = settings.Url;
        store.Save(config);
        console.MarkupLineInterpolated($"[green]Endpoint set to {settings.Url}[/]");
        return 0;
    }
}
