namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// How a hook's command line reaches the operating system.
/// </summary>
public enum HookShell
{
    /// <summary>No shell: the <c>args</c> form, run directly with an argument list.</summary>
    None,

    /// <summary><c>/bin/sh -c &lt;command&gt;</c> on Linux and macOS.</summary>
    PosixSh,

    /// <summary>
    /// <c>powershell.exe -NoProfile -NonInteractive -Command &lt;command&gt;</c> on Windows. The JSON arrives on standard
    /// input, so a PowerShell script reads it from <c>$input</c> (Cursor documents the same).
    /// </summary>
    PowerShell,

    /// <summary><c>cmd.exe /d /s /c &lt;command&gt;</c> on Windows, chosen explicitly.</summary>
    Cmd,
}
