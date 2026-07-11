using System.ComponentModel;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Presets;

public sealed class PresetRemoveCommand(IAnsiConsole console, IPresetStore store) : Command<PresetRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Preset to remove.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.Equals(settings.Name, PtuDefaults.DefaultPresetName, StringComparison.OrdinalIgnoreCase))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] The '{PtuDefaults.DefaultPresetName}' preset cannot be removed. Use 'ptu preset reset' to restore its factory values.");
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

        if (!config.Presets.Remove(settings.Name))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Unknown preset '{settings.Name}'. Run 'ptu preset list' to see available presets.");
            return 1;
        }

        if (string.Equals(config.DefaultPreset, settings.Name, StringComparison.OrdinalIgnoreCase))
        {
            config.DefaultPreset = PtuDefaults.DefaultPresetName;
            if (!config.Presets.ContainsKey(PtuDefaults.DefaultPresetName))
            {
                config.Presets[PtuDefaults.DefaultPresetName] = PtuDefaults.CreateDefaultPreset();
            }
        }

        store.Save(config);
        console.MarkupLineInterpolated($"[green]Removed preset '{settings.Name}'.[/] Active preset: '{config.DefaultPreset}'.");
        return 0;
    }
}
