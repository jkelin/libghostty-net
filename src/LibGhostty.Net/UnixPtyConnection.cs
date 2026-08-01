using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace LibGhostty;

/// <summary>
/// Owns a Unix controlling terminal for a Ghostty PTY session created with forkpty(3).
/// </summary>
public sealed class UnixPtyConnection : IGhosttyPtyConnection
{
    private const int SignalTerm = 15;
    private const int SignalKill = 9;
    private const int WaitBlocking = 0;
    private const int ErrnoInterrupted = 4;
    private const int ErrnoNoChild = 10;
    private static readonly UnixPtyStartDelegate StartNative = LoadNativeStart();

    private readonly object _gate = new();
    private readonly FileStream _stream;
    private readonly int _pid;
    private readonly TaskCompletionSource<int> _exitCompletion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private int _exitCode = -1;
    private bool _hasExited;
    private int _disposeState;
    private EventHandler<GhosttyPtyExitedEventArgs>? _processExited;

    private UnixPtyConnection(int masterFd, int pid)
    {
        _pid = pid;
        var handle = new SafeFileHandle((IntPtr)masterFd, ownsHandle: true);
        try
        {
            _stream = new FileStream(handle, FileAccess.ReadWrite, 4096, isAsync: false);
            _ = Task.Factory.StartNew(
                MonitorProcessExit,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            );
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
    private delegate int UnixPtyStartDelegate(
        out int masterFileDescriptor,
        IntPtr workingDirectory,
        IntPtr executable,
        IntPtr arguments,
        IntPtr environment,
        ushort rows,
        ushort columns
    );

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

    public Stream ReaderStream => _stream;

    public Stream WriterStream => _stream;

    public int Pid => _pid;

    public int ExitCode
    {
        get
        {
            lock (_gate)
            {
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
        ValidateOptions(options);

        var environment = BuildEnvironment(options.Environment);
        var executable = ResolveExecutable(options.App, environment);
        var argv = new NativeUtf8Array([executable, .. options.CommandLine]);
        var envp = new NativeUtf8Array(
            environment.Select(static variable => $"{variable.Key}={variable.Value}")
        );
        var cwd = new NativeUtf8Array([options.Cwd]);
        try
        {
            var startResult = StartNative(
                out var masterFd,
                cwd.First,
                argv.First,
                argv.Pointer,
                envp.Pointer,
                checked((ushort)options.Rows),
                checked((ushort)options.Cols)
            );
            if (startResult < 0)
            {
                throw new IOException($"forkpty/exec helper failed with errno {-startResult}.");
            }

            return Task.FromResult<IGhosttyPtyConnection>(
                new UnixPtyConnection(masterFd, startResult)
            );
        }
        finally
        {
            cwd.Dispose();
            argv.Dispose();
            envp.Dispose();
        }
    }

    public void Resize(int columns, int rows)
    {
        ValidateDimensions(columns, rows);
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_hasExited)
            {
                throw new InvalidOperationException("The terminal process has exited.");
            }
        }

        var window = new WindowSize
        {
            Rows = checked((ushort)rows),
            Columns = checked((ushort)columns),
        };
        var fileDescriptor = _stream.SafeFileHandle.DangerousGetHandle().ToInt32();
        if (Ioctl(fileDescriptor, TerminalWindowSizeRequest(), ref window) != 0)
        {
            throw new IOException(
                $"ioctl(TIOCSWINSZ) failed with errno {Marshal.GetLastWin32Error()}."
            );
        }
    }

    public void Kill()
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_hasExited)
            {
                return;
            }
        }

        // forkpty makes the child a session and process-group leader. A negative
        // pid targets the whole shell/job-control process group, not only the shell.
        var result = KillProcessGroup(SignalTerm);
        var error = Marshal.GetLastWin32Error();
        if (result != 0 && error != ErrnoNoChild)
        {
            throw new IOException($"kill(process group) failed with errno {error}.");
        }
    }

    public bool WaitForExit(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(milliseconds, -1);

        try
        {
            if (!_exitCompletion.Task.Wait(milliseconds))
            {
                return false;
            }
        }
        catch (AggregateException exception)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException ?? exception).Throw();
            throw;
        }

        _exitCompletion.Task.GetAwaiter().GetResult();
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
            lock (_gate)
            {
                if (!_hasExited)
                {
                    _ = KillProcessGroup(SignalKill);
                }
            }

            try
            {
                WaitForExit(1000);
            }
            catch (IOException)
            {
                // The process may already have been reaped by the monitor.
            }
        }
        finally
        {
            _stream.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private void MonitorProcessExit()
    {
        try
        {
            while (true)
            {
                var result = WaitPid(_pid, out var status, WaitBlocking);
                if (result == _pid)
                {
                    CompleteExit(DecodeExitCode(status));
                    return;
                }

                if (result >= 0)
                {
                    continue;
                }

                var error = Marshal.GetLastWin32Error();
                if (error == ErrnoInterrupted)
                {
                    continue;
                }

                if (error == ErrnoNoChild)
                {
                    int exitCode;
                    lock (_gate)
                    {
                        exitCode = _exitCode;
                    }

                    CompleteExit(exitCode);
                    return;
                }

                throw new IOException($"waitpid failed with errno {error}.");
            }
        }
        catch (Exception exception)
        {
            _exitCompletion.TrySetException(exception);
        }
    }

    private void CompleteExit(int exitCode)
    {
        PublishExit(exitCode);
        _exitCompletion.TrySetResult(exitCode);
    }

    private static ulong TerminalWindowSizeRequest() =>
        OperatingSystem.IsMacOS() ? 0x80087467UL : 0x5414UL;

    private static int DecodeExitCode(int status) =>
        (status & 0x7F) == 0 ? (status >> 8) & 0xFF : 128 + (status & 0x7F);

    private static void ValidateOptions(GhosttyPtyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Cwd);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.App);
        ValidateDimensions(options.Cols, options.Rows);
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

    private static Dictionary<string, string> BuildEnvironment(
        IReadOnlyDictionary<string, string> overrides
    )
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                environment[key] = value;
            }
        }

        foreach (var variable in overrides)
        {
            if (
                variable.Key is null
                || variable.Value is null
                || variable.Key.Length == 0
                || variable.Key.Contains('=')
                || variable.Key.Contains('\0')
                || variable.Value.Contains('\0')
            )
            {
                throw new ArgumentException(
                    "Environment names cannot be empty or contain '=' or NUL characters, and values cannot contain NUL characters."
                );
            }

            environment[variable.Key] = variable.Value;
        }

        return environment;
    }

    private static string ResolveExecutable(
        string application,
        Dictionary<string, string> environment
    )
    {
        if (application.Contains('/') || application.Contains('\\'))
        {
            return application;
        }

        var path = environment.TryGetValue("PATH", out var configuredPath)
            ? configuredPath
            : string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator))
        {
            var root = string.IsNullOrEmpty(directory)
                ? Directory.GetCurrentDirectory()
                : directory;
            var candidate = Path.Combine(root, application);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"'{application}' was not found on PATH.", application);
    }

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int Ioctl(int fileDescriptor, ulong request, ref WindowSize windowSize);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int processId, int signal);

    [DllImport("libc", EntryPoint = "waitpid", SetLastError = true)]
    private static extern int WaitPid(int processId, out int status, int options);

    private static UnixPtyStartDelegate LoadNativeStart()
    {
        var library = NativeLibrary.Load(GhosttyRuntimeAssets.ResolvePtyHelper());
        try
        {
            var address = NativeLibrary.GetExport(library, "muxer_forkpty_exec");

            // Keep the library loaded for the lifetime of the function-pointer delegate.
            return Marshal.GetDelegateForFunctionPointer<UnixPtyStartDelegate>(address);
        }
        catch
        {
            NativeLibrary.Free(library);
            throw;
        }
    }

    private void PublishExit(int exitCode)
    {
        EventHandler<GhosttyPtyExitedEventArgs>? handler;
        lock (_gate)
        {
            if (_hasExited)
            {
                return;
            }

            _hasExited = true;
            _exitCode = exitCode;
            handler = _processExited;
        }

        handler?.Invoke(this, new GhosttyPtyExitedEventArgs(exitCode));
    }

    private int KillProcessGroup(int signal) => Kill(-_pid, signal);

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowSize
    {
        public ushort Rows;
        public ushort Columns;
        public ushort HorizontalPixels;
        public ushort VerticalPixels;
    }

    private sealed class NativeUtf8Array : IDisposable
    {
        private readonly List<IntPtr> _strings = [];
        private int _disposeState;

        public NativeUtf8Array(IEnumerable<string> values)
        {
            var materialized = values as IReadOnlyList<string> ?? [.. values];
            Pointer = Marshal.AllocHGlobal((materialized.Count + 1) * IntPtr.Size);
            try
            {
                for (var index = 0; index < materialized.Count; index++)
                {
                    var value = AllocateString(materialized[index]);
                    _strings.Add(value);
                    Marshal.WriteIntPtr(Pointer, index * IntPtr.Size, value);
                }

                Marshal.WriteIntPtr(Pointer, materialized.Count * IntPtr.Size, IntPtr.Zero);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public IntPtr Pointer { get; }

        public IntPtr First => _strings.Count == 0 ? IntPtr.Zero : _strings[0];

        public static IntPtr AllocateString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value + "\0");
            var pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            {
                return;
            }

            foreach (var pointer in _strings)
            {
                Marshal.FreeHGlobal(pointer);
            }

            Marshal.FreeHGlobal(Pointer);
        }
    }
}
