using LibGhostty;

namespace LibGhostty.Net.Tests;

public sealed class ConPtyNativeTests
{
    [WindowsFact]
    public void PublicConPtyBindingLoadsAndDisposesIdempotently()
    {
        var assets = TestAssetLocator.RequireWindowsTerminalAssets();
        var native = new ConPtyNative(assets.ConPtyDll, assets.OpenConsole);

        native.Dispose();
        native.Dispose();
    }

    [Fact]
    public void ConstructorValidatesAssetPaths()
    {
        Assert.Throws<ArgumentException>(() => new ConPtyNative("  ", "  "));

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Throws<FileNotFoundException>(
            () => new ConPtyNative(
                Path.Combine(Path.GetTempPath(), "missing-conpty.dll"),
                Path.Combine(Path.GetTempPath(), "missing-openconsole.exe")
            )
        );
    }
}
