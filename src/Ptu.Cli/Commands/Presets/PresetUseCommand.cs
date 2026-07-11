using System.ComponentModel;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Presets;

public sealed class PresetUseCommand(IAnsiConsole console, IPresetStore store) : Command<PresetUseCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Preset to make active.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
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

        if (!config.Presets.ContainsKey(settings.Name))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Unknown preset '{settings.Name}'. Run 'ptu preset list' to see available presets.");
            return 1;
        }

        config.DefaultPreset = settings.Name;
        store.Save(config);
        console.MarkupLineInterpolated($"[green]Active preset is now '{settings.Name}'.[/]");
        return 0;
    }
}
