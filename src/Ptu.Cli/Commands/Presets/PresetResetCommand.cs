using System.ComponentModel;
using Ptu.Cli.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ptu.Cli.Commands.Presets;

public sealed class PresetResetCommand(IAnsiConsole console, IPresetStore store) : Command<PresetResetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--all")]
        [Description("Also remove every preset other than 'default'.")]
        public bool All { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        PresetConfig config;
        try
        {
            config = store.Load();
        }
        catch (InvalidOperationException)
        {
            // A corrupt configuration must still be resettable.
            config = PtuDefaults.CreateConfig();
        }

        if (settings.All)
        {
            var fresh = PtuDefaults.CreateConfig();
            fresh.ApiEndpoint = config.ApiEndpoint; // Resetting presets must not discard the API endpoint...
            fresh.AuthCookie = config.AuthCookie;   // ...or the auth cookie.
            config = fresh;
        }
        else
        {
            config.Presets[PtuDefaults.DefaultPresetName] = PtuDefaults.CreateDefaultPreset();
            config.DefaultPreset = PtuDefaults.DefaultPresetName;
        }

        store.Save(config);
        console.MarkupLineInterpolated($"[green]Restored the '{PtuDefaults.DefaultPresetName}' preset to factory values and made it active.[/]");
        console.MarkupLineInterpolated($"Learn tab: {config.Presets[PtuDefaults.DefaultPresetName].Tab}");
        if (settings.All)
        {
            console.MarkupLine("[green]Removed all other presets.[/]");
        }

        return 0;
    }
}
