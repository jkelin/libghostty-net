using System;
using System.IO;

namespace LibGhostty;

/// <summary>Owns a process and the byte streams attached to a Ghostty terminal session.</summary>
public interface IGhosttyPtyConnection : IDisposable
{
    event EventHandler<GhosttyPtyExitedEventArgs>? ProcessExited;

    Stream ReaderStream { get; }

    Stream WriterStream { get; }

    int Pid { get; }

    int ExitCode { get; }

    void Resize(int columns, int rows);

    void Kill();

    bool WaitForExit(int milliseconds);
}
