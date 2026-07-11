using System.ComponentModel;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Presets;

public sealed class PresetSetCommand(IAnsiConsole console, IPresetStore store) : Command<PresetSetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Preset to create or update.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--regions <REGIONS>")]
        [Description("Region list. Repeatable or comma-separated. When creating a preset without regions, factory defaults are used.")]
        public string[] Regions { get; init; } = [];

        [CommandOption("--models <MODELS>")]
        [Description("Model list. Repeatable or comma-separated. When creating a preset without models, factory defaults are used.")]
        public string[] Models { get; init; } = [];
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var regions = CommandInput.Normalize(settings.Regions);
        var models = CommandInput.Normalize(settings.Models);
        if (regions.Count == 0 && models.Count == 0)
        {
            console.MarkupLineInterpolated($"[red]Error:[/] Nothing to set. Provide --regions and/or --models.");
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

        if (!config.Presets.TryGetValue(settings.Name, out var preset))
        {
            preset = PtuDefaults.CreateDefaultPreset();
            config.Presets[settings.Name] = preset;
        }

        if (regions.Count > 0)
        {
            preset.Regions = regions;
        }

        if (models.Count > 0)
        {
            preset.Models = models;
        }

        store.Save(config);

        console.MarkupLineInterpolated($"[green]Saved preset '{settings.Name}'.[/]");
        console.MarkupLineInterpolated($"Regions: {string.Join(", ", preset.Regions)}");
        console.MarkupLineInterpolated($"Models: {string.Join(", ", preset.Models)}");
        return 0;
    }
}
