using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace LibGhostty;

/// <summary>
/// Starts a Windows pseudoconsole with the release ConPTY binary built from Windows Terminal.
/// </summary>
public sealed class WindowsPtyConnection : IGhosttyPtyConnection
{
    private const uint ExtendedStartupInfoPresent = 0x0008_0000;
    private const uint CreateUnicodeEnvironment = 0x0000_0400;
    private const int StartfUseStdHandles = 0x0000_0100;
    private const uint ProcThreadAttributePseudoConsole = 0x0002_0016;
    private const uint Infinite = 0xFFFF_FFFF;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 0x0000_0102;
    private const uint WaitFailed = 0xFFFF_FFFF;
    private const uint DuplicateSameAccess = 0x0000_0002;

    private readonly object _gate = new();
    private readonly ConPtyNative _native;
    private readonly IntPtr _pseudoConsole;
    private readonly SafeFileHandle _readerHandle;
    private readonly SafeFileHandle _writerHandle;
    private readonly FileStream _readerStream;
    private readonly FileStream _writerStream;
    private IntPtr _processHandle;
    private IntPtr _exitMonitorHandle;
    private IntPtr _threadHandle;
    private int _exitCode;
    private int _disposeState;
    private bool _hasExited;
    private EventHandler<GhosttyPtyExitedEventArgs>? _processExited;
    private WindowsPtyConnection(
        ConPtyNative native,
        IntPtr pseudoConsole,
        IntPtr processHandle,
        IntPtr exitMonitorHandle,
        IntPtr threadHandle,
        int pid,
        SafeFileHandle readerHandle,
        SafeFileHandle writerHandle
    )
    {
        _native = native;
        _pseudoConsole = pseudoConsole;
        _processHandle = processHandle;
        _exitMonitorHandle = exitMonitorHandle;
        _threadHandle = threadHandle;
        Pid = pid;
        _readerHandle = readerHandle;
        _writerHandle = writerHandle;

        FileStream? readerStream = null;
        FileStream? writerStream = null;
        try
        {
            readerStream = new FileStream(_readerHandle, FileAccess.Read, 4096, isAsync: false);
            writerStream = new FileStream(_writerHandle, FileAccess.Write, 4096, isAsync: false);
            _readerStream = readerStream;
            _writerStream = writerStream;
            _ = Task.Factory.StartNew(
                MonitorProcessExit,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            );
        }
        catch
        {
            writerStream?.Dispose();
            readerStream?.Dispose();
            if (writerStream is null)
            {
                _writerHandle.Dispose();
            }

            if (readerStream is null)
            {
                _readerHandle.Dispose();
            }

            throw;
        }
    }

    event EventHandler<GhosttyPtyExitedEventArgs>? IGhosttyPtyConnection.ProcessExited
    {
        add
        {
            ArgumentNullException.ThrowIfNull(value);
            int? exitCode = null;
            lock (_gate)
            {
                _processExited += value;
                if (_hasExited)
                {
                    exitCode = _exitCode;
                }
            }

            if (exitCode.HasValue)
            {
                value(this, new GhosttyPtyExitedEventArgs(exitCode.Value));
            }
        }
        remove
        {
            lock (_gate)
            {
                _processExited -= value;
            }
        }
    }

    public Stream ReaderStream => _readerStream;

    public Stream WriterStream => _writerStream;

    public int Pid { get; }

    public int ExitCode
    {
        get
        {
            lock (_gate)
            {
                if (
                    _processHandle != IntPtr.Zero
                    && NativeMethods.GetExitCodeProcess(_processHandle, out var exitCode)
                )
                {
                    _exitCode = unchecked((int)exitCode);
                }

                return _exitCode;
            }
        }
    }

    public static Task<IGhosttyPtyConnection> StartAsync(
        GhosttyPtyOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The packaged ConPTY connection is only available on Windows."
            );
        }

        var assets = GhosttyRuntimeAssets.ResolveWindowsTerminalAssets();
        return Task.FromResult<IGhosttyPtyConnection>(Create(options, assets, cancellationToken));
    }

    public void Resize(int columns, int rows)
    {
        ValidateDimensions(columns, rows);
        ThrowIfDisposed();
        var result = _native.ResizePseudoConsole(_pseudoConsole, columns, rows);
        ThrowForHResult(result, "ConptyResizePseudoConsole");
    }

    public void Kill()
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_processHandle != IntPtr.Zero && !NativeMethods.TerminateProcess(_processHandle, 1))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != NativeMethods.ErrorInvalidHandle)
                {
                    throw new Win32Exception(error, "TerminateProcess failed.");
                }
            }
        }
    }

    public bool WaitForExit(int milliseconds)
    {
        ThrowIfDisposed();
        var timeout = milliseconds < 0 ? Infinite : checked((uint)milliseconds);
        var result = NativeMethods.WaitForSingleObject(_processHandle, timeout);
        if (result == WaitTimeout)
        {
            return false;
        }

        if (result == WaitFailed)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WaitForSingleObject failed.");
        }

        if (result != WaitObject0)
        {
            throw new InvalidOperationException($"Unexpected process wait result: {result}.");
        }

        int exitCode;
        lock (_gate)
        {
            if (
                _processHandle == IntPtr.Zero
                || !NativeMethods.GetExitCodeProcess(_processHandle, out var nativeExitCode)
            )
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "GetExitCodeProcess failed after the process exited."
                );
            }

            exitCode = _exitCode = unchecked((int)nativeExitCode);
        }

        PublishExit(exitCode);
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        try
        {
            _readerStream.Dispose();
        }
        catch
        {
            // Best effort: closing the ConPTY is still required during shutdown.
        }
        finally
        {
            _readerHandle.Dispose();
        }

        try
        {
            _writerStream.Dispose();
        }
        catch
        {
            // Best effort: closing the ConPTY is still required during shutdown.
        }
        finally
        {
            _writerHandle.Dispose();
        }

        lock (_gate)
        {
            if (_processHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }

            if (_threadHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_threadHandle);
                _threadHandle = IntPtr.Zero;
            }
        }

        try
        {
            _native.ClosePseudoConsole(_pseudoConsole);
        }
        finally
        {
            _native.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private static WindowsPtyConnection Create(
        GhosttyPtyOptions options,
        WindowsTerminalAssets assets,
        CancellationToken cancellationToken
    )
    {
        ValidateOptions(options);
        ValidateDimensions(options.Cols, options.Rows);

        var native = new ConPtyNative(assets.ConPtyDll, assets.OpenConsole);
        IntPtr pseudoConsole = IntPtr.Zero;
        IntPtr inputRead = IntPtr.Zero;
        IntPtr inputWrite = IntPtr.Zero;
        IntPtr outputRead = IntPtr.Zero;
        IntPtr outputWrite = IntPtr.Zero;
        IntPtr processHandle = IntPtr.Zero;
        IntPtr exitMonitorHandle = IntPtr.Zero;
        IntPtr threadHandle = IntPtr.Zero;
        IntPtr attributeList = IntPtr.Zero;
        IntPtr environmentBlock = IntPtr.Zero;
        IntPtr commandLine = IntPtr.Zero;
        var processStarted = false;
        var connectionCreated = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var securityAttributes = new NativeMethods.SecurityAttributes
            {
                Length = Marshal.SizeOf<NativeMethods.SecurityAttributes>(),
                InheritHandle = 1,
            };
            ThrowIfFalse(
                NativeMethods.CreatePipe(out inputRead, out inputWrite, ref securityAttributes, 0),
                "CreatePipe(input)"
            );
            ThrowIfFalse(
                NativeMethods.CreatePipe(
                    out outputRead,
                    out outputWrite,
                    ref securityAttributes,
                    0
                ),
                "CreatePipe(output)"
            );
            ThrowIfFalse(
                NativeMethods.SetHandleInformation(inputWrite, NativeMethods.HandleFlagInherit, 0),
                "SetHandleInformation(input)"
            );
            ThrowIfFalse(
                NativeMethods.SetHandleInformation(outputRead, NativeMethods.HandleFlagInherit, 0),
                "SetHandleInformation(output)"
            );

            ThrowForHResult(
                native.CreatePseudoConsole(
                    options.Cols,
                    options.Rows,
                    inputRead,
                    outputWrite,
                    out pseudoConsole
                ),
                "ConptyCreatePseudoConsole"
            );

            var attributeListSize = IntPtr.Zero;
            ThrowIfFalse(
                NativeMethods.InitializeProcThreadAttributeList(
                    IntPtr.Zero,
                    1,
                    0,
                    ref attributeListSize
                )
                    || Marshal.GetLastWin32Error() == NativeMethods.ErrorInsufficientBuffer,
                "InitializeProcThreadAttributeList(size)"
            );
            attributeList = Marshal.AllocHGlobal(attributeListSize);
            ThrowIfFalse(
                NativeMethods.InitializeProcThreadAttributeList(
                    attributeList,
                    1,
                    0,
                    ref attributeListSize
                ),
                "InitializeProcThreadAttributeList"
            );
            ThrowIfFalse(
                NativeMethods.UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)ProcThreadAttributePseudoConsole,
                    pseudoConsole,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero
                ),
                "UpdateProcThreadAttribute(PseudoConsole)"
            );

            environmentBlock = Marshal.StringToHGlobalUni(
                BuildEnvironmentBlock(options.Environment)
            );
            var startupInfo = new NativeMethods.StartupInfoEx
            {
                StartupInfo = new NativeMethods.StartupInfo
                {
                    Size = Marshal.SizeOf<NativeMethods.StartupInfoEx>(),

                    // ConPTY clients route standard handles through the pseudoconsole.
                    Flags = StartfUseStdHandles,
                },
                AttributeList = attributeList,
            };
            var processInfo = default(NativeMethods.ProcessInformation);
            commandLine = Marshal.StringToHGlobalUni(BuildCommandLine(options));
            ThrowIfFalse(
                NativeMethods.CreateProcess(
                    applicationName: null,
                    commandLine,
                    processAttributes: IntPtr.Zero,
                    threadAttributes: IntPtr.Zero,
                    inheritHandles: false,
                    creationFlags: ExtendedStartupInfoPresent | CreateUnicodeEnvironment,
                    environment: environmentBlock,
                    currentDirectory: options.Cwd,
                    startupInfo: ref startupInfo,
                    processInformation: out processInfo
                ),
                "CreateProcess(ConPTY child)"
            );
            processHandle = processInfo.Process;
            threadHandle = processInfo.Thread;
            processStarted = true;
            ThrowIfFalse(
                NativeMethods.DuplicateHandle(
                    NativeMethods.GetCurrentProcess(),
                    processHandle,
                    NativeMethods.GetCurrentProcess(),
                    out exitMonitorHandle,
                    0,
                    false,
                    DuplicateSameAccess
                ),
                "DuplicateHandle(process)"
            );
            NativeMethods.CloseHandle(inputRead);
            inputRead = IntPtr.Zero;
            NativeMethods.CloseHandle(outputWrite);
            outputWrite = IntPtr.Zero;
            ThrowForHResult(
                native.ReleasePseudoConsole(pseudoConsole),
                "ConptyReleasePseudoConsole"
            );

            SafeFileHandle? readerHandle = null;
            SafeFileHandle? writerHandle = null;
            try
            {
                readerHandle = TakeOwnedHandle(ref outputRead);
                writerHandle = TakeOwnedHandle(ref inputWrite);
                var connection = new WindowsPtyConnection(
                    native,
                    pseudoConsole,
                    processHandle,
                    exitMonitorHandle,
                    threadHandle,
                    checked((int)processInfo.ProcessId),
                    readerHandle,
                    writerHandle
                );
                exitMonitorHandle = IntPtr.Zero;
                connectionCreated = true;
                return connection;
            }
            catch
            {
                writerHandle?.Dispose();
                readerHandle?.Dispose();
                throw;
            }
        }
        catch
        {
            if (processStarted && processHandle != IntPtr.Zero)
            {
                NativeMethods.TerminateProcess(processHandle, 1);
            }

            if (processHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(processHandle);
            }

            if (threadHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(threadHandle);
            }

            if (pseudoConsole != IntPtr.Zero)
            {
                native.ClosePseudoConsole(pseudoConsole);
            }

            throw;
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (environmentBlock != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(environmentBlock);
            }

            if (commandLine != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(commandLine);
            }

            CloseHandle(ref inputRead);
            CloseHandle(ref inputWrite, except: connectionCreated);
            CloseHandle(ref outputRead, except: connectionCreated);
            CloseHandle(ref outputWrite);
            if (!connectionCreated)
            {
                native.Dispose();
            }
            CloseHandle(ref exitMonitorHandle);
        }
    }

    private static string BuildCommandLine(GhosttyPtyOptions options)
    {
        var builder = new StringBuilder(QuoteArgument(options.App));
        foreach (var argument in options.CommandLine)
        {
            builder.Append(' ');
            builder.Append(options.VerbatimCommandLine ? argument : QuoteArgument(argument));
        }

        return builder.ToString();
    }

    private static string BuildEnvironmentBlock(IReadOnlyDictionary<string, string> overrides)
    {
        var environment = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                environment[key] = value;
            }
        }

        foreach (var pair in overrides)
        {
            if (
                pair.Key is null
                || pair.Value is null
                || pair.Key.Length == 0
                || pair.Key.Contains('=')
                || pair.Key.Contains('\0')
                || pair.Value.Contains('\0')
            )
            {
                throw new ArgumentException(
                    "Environment names cannot be empty or contain '=' or NUL characters, and values cannot contain NUL characters."
                );
            }

            environment[pair.Key] = pair.Value;
        }

        var builder = new StringBuilder();
        foreach (var pair in environment)
        {
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');
        }

        builder.Append('\0');
        return builder.ToString();
    }

    private static string QuoteArgument(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Contains('\0'))
        {
            throw new ArgumentException(
                "Command-line arguments cannot contain NUL characters.",
                nameof(argument)
            );
        }

        if (argument.Length == 0)
        {
            return "\"\"";
        }

        var needsQuotes = false;
        foreach (var character in argument)
        {
            if (char.IsWhiteSpace(character) || character == '"')
            {
                needsQuotes = true;
                break;
            }
        }

        if (!needsQuotes)
        {
            return argument;
        }

        var builder = new StringBuilder(argument.Length + 2);
        builder.Append('"');
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1).Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(character);
        }

        builder.Append('\\', backslashes * 2).Append('"');
        return builder.ToString();
    }

    private void MonitorProcessExit()
    {
        IntPtr monitorHandle;
        lock (_gate)
        {
            monitorHandle = _exitMonitorHandle;
        }

        try
        {
            var result = NativeMethods.WaitForSingleObject(monitorHandle, Infinite);
            if (result != WaitObject0 || Volatile.Read(ref _disposeState) != 0)
            {
                return;
            }

            if (!NativeMethods.GetExitCodeProcess(monitorHandle, out var nativeExitCode))
            {
                return;
            }

            PublishExit(unchecked((int)nativeExitCode));
        }
        finally
        {
            lock (_gate)
            {
                if (_exitMonitorHandle == monitorHandle)
                {
                    _exitMonitorHandle = IntPtr.Zero;
                }
            }

            if (monitorHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(monitorHandle);
            }
        }
    }

    private void PublishExit(int exitCode)
    {
        EventHandler<GhosttyPtyExitedEventArgs>? handler;
        lock (_gate)
        {
            if (_hasExited || Volatile.Read(ref _disposeState) != 0)
            {
                return;
            }

            _hasExited = true;
            _exitCode = exitCode;
            handler = _processExited;
        }

        handler?.Invoke(this, new GhosttyPtyExitedEventArgs(exitCode));
    }

    private static void ValidateOptions(GhosttyPtyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.App);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Cwd);
        if (!Directory.Exists(options.Cwd))
        {
            throw new DirectoryNotFoundException(
                $"Working directory '{options.Cwd}' does not exist."
            );
        }

        ArgumentNullException.ThrowIfNull(options.CommandLine);
        ArgumentNullException.ThrowIfNull(options.Environment);
    }

    private static void ValidateDimensions(int columns, int rows)
    {
        if (columns is < 1 or > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        if (rows is < 1 or > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }
    }

    private static void ThrowIfFalse(bool result, string operation)
    {
        if (!result)
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode, $"{operation} failed (Win32 error {errorCode}).");
        }
    }

    private static void ThrowForHResult(int result, string operation)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private static SafeFileHandle TakeOwnedHandle(ref IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("A ConPTY pipe handle was not created.");
        }

        var ownedHandle = handle;
        handle = IntPtr.Zero;
        return new SafeFileHandle(ownedHandle, ownsHandle: true);
    }

    private static void CloseHandle(ref IntPtr handle, bool except = false)
    {
        if (handle != IntPtr.Zero && !except)
        {
            NativeMethods.CloseHandle(handle);
        }

        handle = IntPtr.Zero;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposeState != 0, this);

    private static class NativeMethods
    {
        internal const int ErrorInsufficientBuffer = 122;
        internal const int ErrorInvalidHandle = 6;
        internal const uint HandleFlagInherit = 1;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(
            out IntPtr readPipe,
            out IntPtr writePipe,
            ref SecurityAttributes attributes,
            int size
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DuplicateHandle(
            IntPtr sourceProcessHandle,
            IntPtr sourceHandle,
            IntPtr targetProcessHandle,
            out IntPtr targetHandle,
            uint desiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            uint options
        );

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(
            IntPtr attributeList,
            int attributeCount,
            int flags,
            ref IntPtr size
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(
            IntPtr attributeList,
            uint flags,
            IntPtr attribute,
            IntPtr value,
            IntPtr size,
            IntPtr previousValue,
            IntPtr returnSize
        );

        [DllImport("kernel32.dll")]
        internal static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcess(
            string? applicationName,
            IntPtr commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref StartupInfoEx startupInfo,
            out ProcessInformation processInformation
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SecurityAttributes
        {
            public int Length;
            public IntPtr Descriptor;
            public int InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StartupInfo
        {
            public int Size;
            public string? Reserved;
            public string? Desktop;
            public string? Title;
            public int X;
            public int Y;
            public int XSize;
            public int YSize;
            public int XCountChars;
            public int YCountChars;
            public int FillAttribute;
            public int Flags;
            public short ShowWindow;
            public short Reserved2;
            public IntPtr Reserved2Pointer;
            public IntPtr StandardInput;
            public IntPtr StandardOutput;
            public IntPtr StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct StartupInfoEx
        {
            public StartupInfo StartupInfo;
            public IntPtr AttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }
    }
}
