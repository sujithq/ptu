namespace Ptu.Cli.Tests;

public class VersionTests
{
    [Fact]
    public void Version_PrintsAssemblyVersion()
    {
        var (app, _, _) = TestHost.Create();

        var result = app.Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.Output.Trim());
    }
}
