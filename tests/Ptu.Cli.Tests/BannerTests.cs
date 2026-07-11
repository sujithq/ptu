using Spectre.Console.Testing;

namespace Ptu.Cli.Tests;

public class BannerTests
{
    [Fact]
    public void WriteBanner_RendersFigletText()
    {
        var console = new TestConsole();

        Ptu.Cli.Program.WriteBanner(console);

        var lines = console.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3, "FIGlet output should span multiple lines.");
        Assert.Contains("_", console.Output);
    }

    [Fact]
    public void Help_ShowsFigletBanner()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("--help");

        Assert.Contains(FirstBannerLine(), result.Output);
        // A blank line separates the banner from the help content.
        Assert.Contains("\n\nUSAGE", result.Output.Replace("\r\n", "\n"));
    }

    [Fact]
    public void SubcommandHelp_ShowsFigletBanner()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("preset", "--help");

        Assert.Contains(FirstBannerLine(), result.Output);
    }

    [Fact]
    public void Availability_DoesNotRenderBannerInCommandOutput()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("availability");

        Assert.DoesNotContain(FirstBannerLine(), result.Output);
    }

    [Theory]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "--help" }, true)]
    [InlineData(new[] { "preset", "-h" }, true)]
    [InlineData(new[] { "availability" }, false)]
    [InlineData(new[] { "--version" }, false)]
    public void IsHelpInvocation_DetectsHelpArguments(string[] args, bool expected)
    {
        Assert.Equal(expected, Ptu.Cli.Program.IsHelpInvocation(args));
    }

    /// <summary>
    /// Second non-empty line of the rendered banner, used as a distinctive marker.
    /// (The first line is unsuitable: CommandAppTester trims leading whitespace
    /// from the start of the captured output.)
    /// </summary>
    private static string FirstBannerLine()
    {
        var console = new TestConsole();
        Ptu.Cli.Program.WriteBanner(console);
        return console.Output
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Skip(1)
            .First()
            .TrimEnd();
    }
}
