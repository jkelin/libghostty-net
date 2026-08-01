using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LibGhostty;

/// <summary>
/// Loads the packaged ConPTY exports without binding the process to a system copy.
/// The adjacent OpenConsole executable is part of the vendored staging contract;
/// Windows Terminal selects that side-by-side host when it is available.
/// </summary>
public sealed class ConPtyNative : IDisposable
{
    private readonly IntPtr _library;
    private readonly CreatePseudoConsoleDelegate _createPseudoConsole;
    private readonly ResizePseudoConsoleDelegate _resizePseudoConsole;
    private readonly ReleasePseudoConsoleDelegate _releasePseudoConsole;
    private readonly ClosePseudoConsoleDelegate _closePseudoConsole;
    private int _disposeState;

    public ConPtyNative(string libraryPath, string openConsolePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(openConsolePath);
        LibraryPath = Path.GetFullPath(libraryPath);
        OpenConsolePath = Path.GetFullPath(openConsolePath);
        if (!File.Exists(LibraryPath))
        {
            throw new FileNotFoundException(
                "The packaged ConPTY library was not found.",
                LibraryPath
            );
        }

        if (!File.Exists(OpenConsolePath))
        {
            throw new FileNotFoundException(
                "The matching release OpenConsole executable was not found.",
                OpenConsolePath
            );
        }

        var libraryDirectory = Path.GetDirectoryName(LibraryPath);
        var openConsoleDirectory = Path.GetDirectoryName(OpenConsolePath);
        if (
            !string.Equals(
                libraryDirectory,
                openConsoleDirectory,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                "The packaged ConPTY library and OpenConsole executable must be staged together."
            );
        }

        _library = NativeLibrary.Load(LibraryPath);
        try
        {
            _createPseudoConsole = Load<CreatePseudoConsoleDelegate>("ConptyCreatePseudoConsole");
            _resizePseudoConsole = Load<ResizePseudoConsoleDelegate>("ConptyResizePseudoConsole");
            _releasePseudoConsole = Load<ReleasePseudoConsoleDelegate>(
                "ConptyReleasePseudoConsole"
            );
            _closePseudoConsole = Load<ClosePseudoConsoleDelegate>("ConptyClosePseudoConsole");
        }
        catch
        {
            NativeLibrary.Free(_library);
            throw;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int CreatePseudoConsoleDelegate(
        Coord size,
        IntPtr input,
        IntPtr output,
        uint flags,
        out IntPtr pseudoConsole
    );

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int ResizePseudoConsoleDelegate(IntPtr pseudoConsole, Coord size);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int ReleasePseudoConsoleDelegate(IntPtr pseudoConsole);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void ClosePseudoConsoleDelegate(IntPtr pseudoConsole);

    public string LibraryPath { get; }

    public string OpenConsolePath { get; }

    public int CreatePseudoConsole(
        int columns,
        int rows,
        IntPtr input,
        IntPtr output,
        out IntPtr pseudoConsole
    ) =>
        _createPseudoConsole(
            new Coord(checked((short)columns), checked((short)rows)),
            input,
            output,
            0,
            out pseudoConsole
        );

    public int ResizePseudoConsole(IntPtr pseudoConsole, int columns, int rows) =>
        _resizePseudoConsole(
            pseudoConsole,
            new Coord(checked((short)columns), checked((short)rows))
        );

    public int ReleasePseudoConsole(IntPtr pseudoConsole) => _releasePseudoConsole(pseudoConsole);

    public void ClosePseudoConsole(IntPtr pseudoConsole)
    {
        if (pseudoConsole != IntPtr.Zero)
        {
            _closePseudoConsole(pseudoConsole);
        }
    }

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposeState, 1) == 0)
        {
            NativeLibrary.Free(_library);
        }

        GC.SuppressFinalize(this);
    }

    private T Load<T>(string name)
        where T : Delegate
    {
        try
        {
            return Marshal.GetDelegateForFunctionPointer<T>(
                NativeLibrary.GetExport(_library, name)
            );
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or ArgumentException)
        {
            throw new EntryPointNotFoundException(
                $"ConPTY library at '{LibraryPath}' does not export '{name}'.",
                ex
            );
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Coord
    {
        public readonly short X;
        public readonly short Y;

        public Coord(short x, short y)
        {
            X = x;
            Y = y;
        }
    }
}
