using System.Text;
using LibGhostty;

namespace LibGhostty.Net.Tests;

internal static class TestAssetLocator
{
    public static string NativeDirectory
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("LIBGHOSTTY_NATIVE_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured);
            }

            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (var depth = 0; directory is not null && depth <= 8; depth++)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "artifacts",
                    "native",
                    GhosttyRuntimeAssets.RuntimeIdentifier
                );
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return AppContext.BaseDirectory;
        }
    }

    public static string RequireGhosttyLibrary()
    {
        var path = Path.Combine(NativeDirectory, GhosttyRuntimeAssets.GhosttyLibraryFileName);
        return RequireFile(path, "Ghostty VT library");
    }

    public static string RequirePtyHelper()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Unix PTY tests require a non-Windows host.");
        }

        var path = Path.Combine(NativeDirectory, GhosttyRuntimeAssets.PtyHelperFileName);
        return RequireFile(path, "Unix PTY helper");
    }

    public static WindowsTerminalAssets RequireWindowsTerminalAssets()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ConPTY tests require Windows.");
        }

        var conPty = Path.Combine(NativeDirectory, "conpty.dll");
        var openConsole = Path.Combine(NativeDirectory, "OpenConsole.exe");
        RequireFile(conPty, "ConPTY library");
        RequireFile(openConsole, "OpenConsole host");
        return new WindowsTerminalAssets(NativeDirectory, conPty, openConsole);
    }

    public static string RequireFile(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{description} was not staged at '{path}'. Build the native test runtime first.",
                path
            );
        }

        return Path.GetFullPath(path);
    }

    public static async Task<string> ReadUntilAsync(
        Stream stream,
        string marker,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var buffer = new byte[4096];
        var output = new StringBuilder();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, timeoutSource.Token);
            if (read <= 0)
            {
                return output.ToString();
            }

            output.Append(Encoding.UTF8.GetString(buffer, 0, read));
            if (output.ToString().Contains(marker, StringComparison.Ordinal))
            {
                return output.ToString();
            }
        }
    }
}
