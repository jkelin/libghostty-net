using System;

namespace LibGhostty;

/// <summary>Reports the process exit code from a Ghostty PTY connection.</summary>
public sealed class GhosttyPtyExitedEventArgs : EventArgs
{
    public GhosttyPtyExitedEventArgs(int exitCode)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
