using Xunit;

namespace LibGhostty.Net.Tests;

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "This test requires Windows.";
        }
    }
}

public sealed class SupportedPtyFactAttribute : FactAttribute
{
    public SupportedPtyFactAttribute()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Skip = "PTY tests require Windows, Linux, or macOS.";
        }
    }
}
