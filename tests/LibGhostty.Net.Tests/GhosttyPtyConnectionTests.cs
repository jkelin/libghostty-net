using LibGhostty;

namespace LibGhostty.Net.Tests;

public sealed class GhosttyPtyConnectionTests
{
    private const string EchoMarker = "libghostty-test-pty-ok";

    [SupportedPtyFact]
    public async Task StartsResizesReadsAndRaisesExitWithoutExplicitWait()
    {
        using var connection = await GhosttyPtyConnectionFactory.StartAsync(CreateEchoOptions());
        var exit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ProcessExited += (_, eventArgs) => exit.TrySetResult(eventArgs.ExitCode);

        connection.Resize(100, 30);
        var output = await TestAssetLocator.ReadUntilAsync(
            connection.ReaderStream,
            EchoMarker,
            TimeSpan.FromSeconds(10)
        );

        Assert.Contains(EchoMarker, output, StringComparison.Ordinal);
        var completed = await Task.WhenAny(exit.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(exit.Task, completed);
        Assert.Equal(0, await exit.Task);
        Assert.True(connection.WaitForExit(0));

        connection.Dispose();
        connection.Dispose();
    }

    [SupportedPtyFact]
    public async Task KillRaisesExitForRunningProcess()
    {
        using var connection = await GhosttyPtyConnectionFactory.StartAsync(CreateLongRunningOptions());
        var exit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ProcessExited += (_, eventArgs) => exit.TrySetResult(eventArgs.ExitCode);

        connection.Kill();

        var completed = await Task.WhenAny(exit.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(exit.Task, completed);
        Assert.NotEqual(0, await exit.Task);
    }

    [Fact]
    public void NullOptionsAreRejectedAtFactoryBoundary()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = GhosttyPtyConnectionFactory.StartAsync(null!);
        });
    }

    [Fact]
    public void InvalidDimensionsAreRejectedBeforeProcessCreation()
    {
        var options = CreateEchoOptions();
        options = new GhosttyPtyOptions
        {
            Cols = 0,
            Rows = options.Rows,
            Cwd = options.Cwd,
            App = options.App,
            CommandLine = options.CommandLine,
            Environment = options.Environment,
            VerbatimCommandLine = options.VerbatimCommandLine,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = GhosttyPtyConnectionFactory.StartAsync(options);
        });
    }

    private static GhosttyPtyOptions CreateEchoOptions()
    {
        if (OperatingSystem.IsWindows())
        {
            TestAssetLocator.RequireWindowsTerminalAssets();
            return new GhosttyPtyOptions
            {
                Cols = 80,
                Rows = 24,
                Cwd = Environment.CurrentDirectory,
                App = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                CommandLine = ["/d", "/c", "echo", EchoMarker],
            };
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            TestAssetLocator.RequirePtyHelper();
            return new GhosttyPtyOptions
            {
                Cols = 80,
                Rows = 24,
                Cwd = Environment.CurrentDirectory,
                App = "/bin/sh",
                CommandLine = ["-c", $"printf {EchoMarker}"],
            };
        }

        throw new PlatformNotSupportedException("PTY tests require Windows, Linux, or macOS.");
    }

    private static GhosttyPtyOptions CreateLongRunningOptions()
    {
        if (OperatingSystem.IsWindows())
        {
            TestAssetLocator.RequireWindowsTerminalAssets();
            return new GhosttyPtyOptions
            {
                Cols = 80,
                Rows = 24,
                Cwd = Environment.CurrentDirectory,
                App = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                VerbatimCommandLine = true,
                CommandLine = ["/d /c ping 127.0.0.1 -n 30 >nul"],
            };
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            TestAssetLocator.RequirePtyHelper();
            return new GhosttyPtyOptions
            {
                Cols = 80,
                Rows = 24,
                Cwd = Environment.CurrentDirectory,
                App = "/bin/sh",
                CommandLine = ["-c", "sleep 30"],
            };
        }

        throw new PlatformNotSupportedException("PTY tests require Windows, Linux, or macOS.");
    }
}
