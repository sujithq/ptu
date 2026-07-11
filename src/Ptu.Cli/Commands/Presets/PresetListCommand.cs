using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Presets;

public sealed class PresetListCommand(IAnsiConsole console, IPresetStore store) : Command<EmptyCommandSettings>
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

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Preset");
        table.AddColumn("Regions");
        table.AddColumn("Models");
        table.Caption("* = active preset");

        foreach (var (name, preset) in config.Presets.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var isActive = string.Equals(name, config.DefaultPreset, StringComparison.OrdinalIgnoreCase);
            table.AddRow(
                Markup.Escape(isActive ? $"* {name}" : name),
                Markup.Escape(string.Join(", ", preset.Regions)),
                Markup.Escape(string.Join(", ", preset.Models)));
        }

        console.Write(table);
        return 0;
    }
}
