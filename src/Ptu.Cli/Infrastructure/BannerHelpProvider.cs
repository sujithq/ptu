using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Ptu.Cli.Infrastructure;

/// <summary>Default help output with the ptu FIGlet banner on top.</summary>
public sealed class BannerHelpProvider(ICommandAppSettings settings) : HelpProvider(settings)
{
    public override IEnumerable<IRenderable> GetHeader(ICommandModel model, ICommandInfo? command)
    {
        yield return Program.CreateBanner();
        yield return Text.NewLine;
        foreach (var renderable in base.GetHeader(model, command))
        {
            yield return renderable;
        }
    }
}
