using System.Runtime.InteropServices;

namespace LibGhostty;

/// <summary>Resolves native assets staged by the NuGet package or a local build.</summary>
public static class GhosttyRuntimeAssets
{
    private const string NativeDirectoryEnvironmentVariable = "LIBGHOSTTY_NATIVE_DIR";

    /// <summary>Gets the current process runtime identifier supported by the package.</summary>
    public static string RuntimeIdentifier => ResolveRuntimeIdentifier();

    /// <summary>Gets the Ghostty dynamic library filename for the current operating system.</summary>
    public static string GhosttyLibraryFileName =>
        OperatingSystem.IsWindows() ? "ghostty-vt.dll"
        : OperatingSystem.IsMacOS() ? "libghostty-vt.dylib"
        : "libghostty-vt.so";

    /// <summary>Gets the Unix PTY helper filename for the current operating system.</summary>
    public static string PtyHelperFileName =>
        OperatingSystem.IsMacOS() ? "libmuxer-pty.dylib" : "libmuxer-pty.so";

    /// <summary>Finds the packaged or explicitly staged Ghostty library.</summary>
    public static string ResolveGhosttyLibrary(string? explicitPath = null)
    {
        foreach (var candidate in EnumerateCandidates(explicitPath, GhosttyLibraryFileName))
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        // An explicit path is intentionally not combined with package directories; a typo must fail fast.
        throw new FileNotFoundException(
            $"{GhosttyLibraryFileName} was not found for runtime '{RuntimeIdentifier}'.",
            explicitPath ?? GhosttyLibraryFileName
        );
    }

    /// <summary>Finds the packaged or explicitly staged Unix PTY helper.</summary>
    public static string ResolvePtyHelper(string? explicitPath = null)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The Unix PTY helper is unavailable on Windows, which uses ConPTY."
            );
        }

        foreach (var candidate in EnumerateCandidates(explicitPath, PtyHelperFileName))
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        // An explicit path is intentionally not combined with package directories; a typo must fail fast.
        throw new FileNotFoundException(
            $"{PtyHelperFileName} was not found for runtime '{RuntimeIdentifier}'.",
            explicitPath ?? PtyHelperFileName
        );
    }

    /// <summary>Finds the co-located Windows Terminal ConPTY assets.</summary>
    public static WindowsTerminalAssets ResolveWindowsTerminalAssets()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows Terminal assets are only available on Windows."
            );
        }

        var searched = new List<string>();
        foreach (var directory in EnumerateNativeDirectories())
        {
            var fullDirectory = Path.GetFullPath(directory);
            searched.Add(fullDirectory);
            var conpty = Path.Combine(fullDirectory, "conpty.dll");
            var openConsole = Path.Combine(fullDirectory, "OpenConsole.exe");
            if (File.Exists(conpty) && File.Exists(openConsole))
            {
                return new WindowsTerminalAssets(fullDirectory, conpty, openConsole);
            }
        }

        throw new FileNotFoundException(
            "The packaged Windows Terminal assets were not found. Expected conpty.dll and OpenConsole.exe "
                + $"in one native asset directory. Searched: {string.Join(", ", searched.Distinct(StringComparer.OrdinalIgnoreCase))}",
            "conpty.dll"
        );
    }

    private static IEnumerable<string> EnumerateCandidates(string? explicitPath, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return explicitPath.Trim();
            yield break;
        }

        foreach (var directory in EnumerateNativeDirectories())
        {
            yield return Path.Combine(directory, fileName);
        }
    }

    private static IEnumerable<string> EnumerateNativeDirectories()
    {
        var explicitDirectory = Environment.GetEnvironmentVariable(
            NativeDirectoryEnvironmentVariable
        );
        if (!string.IsNullOrWhiteSpace(explicitDirectory))
        {
            yield return explicitDirectory.Trim();
        }

        yield return AppContext.BaseDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "terminal-native");

        foreach (var root in EnumerateRepositoryRoots())
        {
            yield return Path.Combine(root, "artifacts", "native", RuntimeIdentifier);
        }
    }

    private static IEnumerable<string> EnumerateRepositoryRoots()
    {
        var candidates = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var candidate in candidates)
        {
            var directory = new DirectoryInfo(candidate);
            for (var depth = 0; directory is not null && depth <= 8; depth++)
            {
                yield return directory.FullName;
                directory = directory.Parent;
            }
        }
    }

    private static string ResolveRuntimeIdentifier()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported terminal architecture: {RuntimeInformation.ProcessArchitecture}."
            ),
        };

        if (OperatingSystem.IsWindows())
        {
            if (architecture != "x64")
            {
                throw new PlatformNotSupportedException(
                    $"Unsupported Windows terminal architecture: {architecture}. Only x64 is supported."
                );
            }

            return "win-x64";
        }

        if (OperatingSystem.IsLinux())
        {
            return $"linux-{architecture}";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"osx-{architecture}";
        }

        throw new PlatformNotSupportedException(
            $"Unsupported terminal operating system: {Environment.OSVersion.Platform}."
        );
    }
}
