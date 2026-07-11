using Ptu.Cli.Infrastructure;

namespace Ptu.Cli.Tests;

public sealed class FileSessionMarkerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ptu-tests", Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public FileSessionMarkerTests()
    {
        _path = Path.Combine(_directory, "session");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void TryMarkFirstRun_FirstRunInSession_ReturnsTrue()
    {
        var marker = new FileSessionMarker(_path, () => "session-a");

        Assert.True(marker.TryMarkFirstRun());
    }

    [Fact]
    public void TryMarkFirstRun_SecondRunInSameSession_ReturnsFalse()
    {
        var marker = new FileSessionMarker(_path, () => "session-a");
        marker.TryMarkFirstRun();

        Assert.False(marker.TryMarkFirstRun());
        Assert.False(new FileSessionMarker(_path, () => "session-a").TryMarkFirstRun());
    }

    [Fact]
    public void TryMarkFirstRun_NewSession_ReturnsTrueAgain()
    {
        Assert.True(new FileSessionMarker(_path, () => "session-a").TryMarkFirstRun());
        Assert.True(new FileSessionMarker(_path, () => "session-b").TryMarkFirstRun());
        Assert.False(new FileSessionMarker(_path, () => "session-b").TryMarkFirstRun());
    }

    [Fact]
    public void GetTerminalSessionKey_ReturnsNonEmptyKey()
    {
        Assert.False(string.IsNullOrWhiteSpace(FileSessionMarker.GetTerminalSessionKey()));
    }
}
