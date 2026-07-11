namespace Ptu.Cli.Infrastructure;

/// <summary>Tracks whether the CLI has already run in the current terminal session.</summary>
public sealed class FileSessionMarker(string path, Func<string> sessionKey)
{
    public FileSessionMarker()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ptu", "session"),
            GetTerminalSessionKey)
    {
    }

    /// <summary>Returns true on the first run in the current session and records it; false on subsequent runs.</summary>
    public bool TryMarkFirstRun()
    {
        try
        {
            var key = sessionKey();
            if (File.Exists(path) && string.Equals(File.ReadAllText(path), key, StringComparison.Ordinal))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, key);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Banner bookkeeping must never break the CLI; skip the banner instead.
            return false;
        }
    }

    /// <summary>Best-effort per-terminal session identity, falling back to a per-boot key.</summary>
    public static string GetTerminalSessionKey()
    {
        var key = Environment.GetEnvironmentVariable("WT_SESSION")            // Windows Terminal tab
            ?? Environment.GetEnvironmentVariable("TERM_SESSION_ID")          // macOS Terminal
            ?? Environment.GetEnvironmentVariable("ITERM_SESSION_ID")         // iTerm2
            ?? Environment.GetEnvironmentVariable("VSCODE_GIT_IPC_HANDLE");   // VS Code window

        if (!string.IsNullOrEmpty(key))
        {
            return key;
        }

        // No terminal identity available: treat each machine boot as one session.
        var bootTime = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        return $"boot:{bootTime.ToUnixTimeSeconds() / 60}";
    }
}
