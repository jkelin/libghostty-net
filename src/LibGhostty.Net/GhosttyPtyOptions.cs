using System;
using System.Collections.Generic;

namespace LibGhostty;

/// <summary>Validated process and environment inputs for a Ghostty PTY connection.</summary>
public sealed class GhosttyPtyOptions
{
    public string Name { get; init; } = string.Empty;

    public int Cols { get; init; }

    public int Rows { get; init; }

    public string Cwd { get; init; } = string.Empty;

    public string App { get; init; } = string.Empty;

    public IReadOnlyList<string> CommandLine { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool VerbatimCommandLine { get; init; }
}
