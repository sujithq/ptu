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
}
