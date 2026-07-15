using System.ComponentModel;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Presets;

public sealed class PresetShowCommand(IAnsiConsole console, IPresetStore store) : Command<PresetShowCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[NAME]")]
        [Description("Preset to show. Defaults to the active preset.")]
        public string? Name { get; init; }
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

        var name = settings.Name ?? config.DefaultPreset;
        if (!config.Presets.TryGetValue(name, out var preset))
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Unknown preset '{name}'. Run 'ptu preset list' to see available presets.");
            return 1;
        }

        var isActive = string.Equals(name, config.DefaultPreset, StringComparison.OrdinalIgnoreCase);
        console.MarkupLineInterpolated($"Preset: [bold]{name}[/]{(isActive ? " (active)" : "")}");
        console.MarkupLineInterpolated($"Regions: {string.Join(", ", preset.Regions)}");
        console.MarkupLineInterpolated($"Models: {string.Join(", ", preset.Models)}");
        console.MarkupLineInterpolated($"Learn tab: {preset.Tab}");
        return 0;
    }
}
