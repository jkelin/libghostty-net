namespace LibGhostty;

/// <summary>Co-located Windows Terminal binaries required by the ConPTY integration.</summary>
public readonly record struct WindowsTerminalAssets(
    string Directory,
    string ConPtyDll,
    string OpenConsole
);
