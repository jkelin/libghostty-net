# LibGhostty.Net

[![NuGet version](https://img.shields.io/nuget/v/LibGhostty.Net?logo=nuget)](https://www.nuget.org/packages/LibGhostty.Net/)
[![NuGet downloads](https://img.shields.io/nuget/dt/LibGhostty.Net?logo=nuget)](https://www.nuget.org/packages/LibGhostty.Net/)

LibGhostty.Net is a cross-platform .NET 10 library for building terminal experiences on top of [Ghostty](https://ghostty.org/)'s virtual-terminal engine.

It provides two related layers:

- A managed, low-level binding to Ghostty's native VT C ABI. It can create and resize terminal state, write terminal output, encode keyboard and mouse input, encode bracketed paste, and inspect render state, colors, rows, cells, and cursor data.
- A platform-native pseudo-terminal (PTY) process host. It launches a child process with connected streams, resizes the terminal, terminates the process, waits for exit, and reports exit events.

The PTY layer and the VT layer are intentionally separate. A typical terminal application reads bytes from a PTY, writes those bytes into a Ghostty terminal, renders the resulting state, and writes encoded user input back to the PTY.

## Supported runtimes

The NuGet package contains the native assets needed by these runtime identifiers:

| Runtime | Native components | Notes |
| --- | --- | --- |
| `win-x64` | Ghostty VT, ConPTY, Windows Terminal `OpenConsole.exe` | Windows x64 is currently supported. |
| `linux-x64` | Ghostty VT, Unix PTY helper | |
| `linux-arm64` | Ghostty VT, Unix PTY helper | |
| `osx-x64` | Ghostty VT, Unix PTY helper | Minimum macOS deployment target: 11.0. |
| `osx-arm64` | Ghostty VT, Unix PTY helper | Minimum macOS deployment target: 11.0. |

The managed assembly targets `net10.0`. Native assets are placed in the standard NuGet `runtimes/<rid>/native/` directories and are resolved automatically from the package output directory.

## Installation

The latest release is available on [NuGet](https://www.nuget.org/packages/LibGhostty.Net/):

```sh
dotnet add package LibGhostty.Net
```

To install the published 1.0.1 release explicitly:

```sh
dotnet add package LibGhostty.Net --version 1.0.1
```

For a local package produced from this repository, use the package output directory as a source:

```powershell
dotnet add path\to\YourProject.csproj package LibGhostty.Net `
  --version 1.0.1 `
  --source "path\to\libghostty-net\artifacts\packages"
```

The package includes the managed API and the native runtime assets; applications should not copy the Ghostty or PTY binaries manually.

## Starting a process in a native PTY

`GhosttyPtyConnectionFactory` selects the platform implementation at runtime:

- Linux and macOS use the bundled Unix PTY helper.
- Windows uses the bundled ConPTY and Windows Terminal assets.

```csharp
using LibGhostty;
using System.Text;

var isWindows = OperatingSystem.IsWindows();
var commandLine = isWindows
    ? new[] { "/d", "/c", "echo LibGhostty.Net" }
    : new[] { "-c", "printf 'LibGhostty.Net\\n'" };

var options = new GhosttyPtyOptions
{
    Name = "readme-example",
    Cols = 100,
    Rows = 30,
    Cwd = Environment.CurrentDirectory,
    App = isWindows
        ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
        : "/bin/sh",
    CommandLine = commandLine,
};

using var connection = await GhosttyPtyConnectionFactory.StartAsync(options);
connection.ProcessExited += (_, eventArgs) =>
    Console.WriteLine($"Process exited with code {eventArgs.ExitCode}.");

using var output = new MemoryStream();
await connection.ReaderStream.CopyToAsync(output);
Console.WriteLine(Encoding.UTF8.GetString(output.ToArray()));
```

`IGhosttyPtyConnection` exposes:

- `ReaderStream` and `WriterStream` for process I/O.
- `Pid` and `ExitCode` for process information.
- `Resize(columns, rows)` for terminal-size changes.
- `Kill()` and `WaitForExit(milliseconds)` for lifecycle control.
- `ProcessExited` for asynchronous exit notification.

`GhosttyPtyOptions` controls the executable, arguments, working directory, initial dimensions, environment variables, and whether the command line should be passed verbatim on Windows. Invalid dimensions, missing executables, unsupported platforms, and native process errors fail at the API boundary.

## Using the Ghostty VT binding

`GhosttyNativeLibrary` is the advanced, low-level API. It loads the packaged Ghostty library, resolves the exported C ABI symbols, and exposes native-compatible structs and status codes.

```csharp
using LibGhostty;

using var ghostty = GhosttyNativeLibrary.LoadFromPackage();

var result = ghostty.TerminalNew(
    new GhosttyNativeLibrary.TerminalOptions
    {
        Columns = 100,
        Rows = 30,
        MaxScrollback = 10_000,
    },
    out var terminal
);

if (result != GhosttyNativeLibrary.Success)
{
    throw new InvalidOperationException($"Ghostty terminal creation failed: {result}.");
}

try
{
    // Feed terminal output into the native terminal with TerminalWrite.
    // Use RenderStateNew, RenderStateUpdate, and the row/cell APIs to render it.
}
finally
{
    ghostty.TerminalFree(terminal);
}
```

The binding uses opaque `IntPtr` handles for native terminal, encoder, event, render-state, row-iterator, and row-cell objects. The caller owns those handles and must free them with the corresponding API before disposing `GhosttyNativeLibrary`. Native methods return Ghostty status codes; `GhosttyNativeLibrary.Success` is zero.

### VT functionality exposed

The managed facade exposes the following groups of Ghostty functionality:

- Terminal lifecycle and configuration: create, free, resize, set options, write VT data, read title, scrollbar state, mouse tracking, and active screen.
- Keyboard input: create and configure key events, configure a key encoder, and encode input bytes.
- Mouse input: create and configure mouse events, configure a mouse encoder, and encode input bytes.
- Paste: encode bracketed or unbracketed paste data.
- Scrolling: set the terminal scroll viewport.
- Rendering: create/update/free render state, query dimensions and cursor state, retrieve colors, iterate rows, inspect raw row/cell values, inspect styles, and query wide-cell state.
- Native callbacks and ABI structs: callback delegates, terminal options, colors, styles, positions, sizes, strings, scrollbars, and render-state values.

This layer is intended for terminal UI implementations that already understand unmanaged memory, native handles, and the Ghostty ABI. The PTY layer is the simpler entry point for applications that only need to launch a process and exchange bytes.

## Runtime asset resolution

`GhosttyRuntimeAssets` provides the runtime-facing asset lookup used by the library:

- `RuntimeIdentifier` reports the current supported RID.
- `ResolveGhosttyLibrary()` locates `ghostty-vt.dll`, `libghostty-vt.so`, or `libghostty-vt.dylib`.
- `ResolvePtyHelper()` locates the Unix PTY helper and throws `PlatformNotSupportedException` on Windows.
- `ResolveWindowsTerminalAssets()` locates `conpty.dll` and `OpenConsole.exe` on Windows.

For local native development, set `LIBGHOSTTY_NATIVE_DIR` to a directory containing the assets for the current runtime. An explicit path can also be passed to the resolver methods. Missing assets fail fast with a descriptive exception rather than silently selecting a different runtime.

## Building from source

Build and native asset instructions, including the macOS SDK workflow, are maintained in [BUILDING.md](BUILDING.md).

## Native components and licensing

This package redistributes native components from the Ghostty fork and the Windows Terminal integration used by this project. Review the licenses and notices in [`native/ghostty`](native/ghostty) and [`native/windows-terminal`](native/windows-terminal) before redistributing the package.

## License

The LibGhostty.Net project is licensed under the [MIT License](LICENSE). The redistributed Ghostty and Windows Terminal components retain their own licenses; see the notices under [`native/ghostty`](native/ghostty) and [`native/windows-terminal`](native/windows-terminal).
