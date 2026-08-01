namespace LibGhostty;

/// <summary>Starts a native PTY using the host operating system implementation.</summary>
public static class GhosttyPtyConnectionFactory
{
    /// <summary>Starts a process attached to a platform-native PTY.</summary>
    public static Task<IGhosttyPtyConnection> StartAsync(
        GhosttyPtyOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        return OperatingSystem.IsWindows()
            ? WindowsPtyConnection.StartAsync(options, cancellationToken)
            : UnixPtyConnection.StartAsync(options, cancellationToken);
    }
}
