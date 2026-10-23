using LibGhostty;

namespace LibGhostty.Net.Tests;

public sealed class GhosttyRuntimeAssetsTests
{
	[Theory]
	[InlineData(true, false, false, System.Runtime.InteropServices.Architecture.X64, "win-x64")]
	[InlineData(true, false, false, System.Runtime.InteropServices.Architecture.Arm64, "win-arm64")]
	[InlineData(false, true, false, System.Runtime.InteropServices.Architecture.Arm64, "linux-arm64")]
	[InlineData(false, false, true, System.Runtime.InteropServices.Architecture.Arm64, "osx-arm64")]
	public void RuntimeIdentifierUsesTheOperatingSystemAndProcessArchitecture(
		bool isWindows,
		bool isLinux,
		bool isMacOS,
		System.Runtime.InteropServices.Architecture architecture,
		string expected
	)
	{
		Assert.Equal(expected, GhosttyRuntimeAssets.ResolveRuntimeIdentifier(isWindows, isLinux, isMacOS, architecture));
	}

    [Fact]
    public void ExplicitGhosttyPathIsNormalizedAndReturned()
    {
        var directory = Directory.CreateTempSubdirectory("libghostty-assets-");
        try
        {
            var file = Path.Combine(directory.FullName, "ghostty-vt.test");
            File.WriteAllBytes(file, [1, 2, 3]);

            var resolved = GhosttyRuntimeAssets.ResolveGhosttyLibrary(file);

            Assert.Equal(Path.GetFullPath(file), resolved);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void MissingExplicitGhosttyPathFailsFast()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "ghostty-vt.dll");

        Assert.Throws<FileNotFoundException>(() => GhosttyRuntimeAssets.ResolveGhosttyLibrary(missing));
    }

    [Fact]
    public void ExplicitPtyPathIsNormalizedAndReturnedOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(() => GhosttyRuntimeAssets.ResolvePtyHelper());
            return;
        }

        var directory = Directory.CreateTempSubdirectory("libghostty-pty-");
        try
        {
            var file = Path.Combine(directory.FullName, GhosttyRuntimeAssets.PtyHelperFileName);
            File.WriteAllBytes(file, [1, 2, 3]);

            var resolved = GhosttyRuntimeAssets.ResolvePtyHelper(file);

            Assert.Equal(Path.GetFullPath(file), resolved);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void WindowsAssetsAreRejectedOnNonWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Throws<PlatformNotSupportedException>(
                () => GhosttyRuntimeAssets.ResolveWindowsTerminalAssets()
            );
        }
    }
}
