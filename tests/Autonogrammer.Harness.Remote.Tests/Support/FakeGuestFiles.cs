using System.ComponentModel;
using System.Diagnostics;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>File helpers the fake container and WSL hosts share.</summary>
internal static class FakeGuestFiles
{
    private const string ProbeVariable = "AUTONOGRAMMER_TEST_EXEC_PROBE";
    private const string ShellShebang = "#!/bin/sh\n";
    private const int TextFileBusy = 26;
    private const int MaximumProbeAttempts = 400;

    /// <summary>
    /// Writes a script and makes it executable. Linux refuses to execute a file that any process still holds open for writing (ETXTBSY, "Text file busy"):
    /// a child that another test forked while this file was being written keeps that descriptor until it reaches <c>exec</c>, which on a loaded machine can
    /// take seconds, so a test that ran the wrapper in that window failed by chance. A shell wrapper therefore starts with a line that makes it exit at once
    /// when it is run as a probe, and this method runs that probe until the kernel accepts the file.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="content">The text.</param>
    public static void WriteExecutable(string path, string content)
    {
        var isShell = content.StartsWith(ShellShebang, StringComparison.Ordinal);
        File.WriteAllText(path, isShell ? content.Insert(ShellShebang.Length, "[ \"${" + ProbeVariable + ":-}\" = 1 ] && exit 0\n") : content);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (isShell)
        {
            WaitUntilExecutable(path);
        }
    }

    private static void WaitUntilExecutable(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var start = new ProcessStartInfo(path) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.Environment[ProbeVariable] = "1";
                using var probe = Process.Start(start) ?? throw new InvalidOperationException("The probe of " + path + " did not start.");
                _ = probe.WaitForExit(TimeSpan.FromSeconds(30));
                return;
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == TextFileBusy && attempt < MaximumProbeAttempts)
            {
                Thread.Sleep(25);
            }
        }
    }

    /// <summary>Quotes a value for the wrapper scripts the fake hosts write.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The single-quoted word.</returns>
    public static string Quote(string value) => PosixQuoting.Quote(value);

    /// <summary>Deletes a directory tree; a leftover temporary directory does not fail a test.</summary>
    /// <param name="path">The directory.</param>
    public static void Delete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory does not fail a test.
        }
    }

    /// <summary>Installs the fake omp as <c>.bun/bin/omp</c> under a home directory and a <c>uname</c> override under a bin directory.</summary>
    /// <param name="home">The home directory of the "guest" user.</param>
    /// <param name="overrideBin">A directory that comes first on the guest PATH.</param>
    /// <param name="installOmp">Whether to install omp.</param>
    /// <param name="uname">The text a fake <c>uname -sm</c> prints, or <see langword="null"/> to use the real one.</param>
    public static void InstallTools(string home, string overrideBin, bool installOmp, string? uname)
    {
        var binDirectory = Path.Combine(home, ".bun", "bin");
        _ = Directory.CreateDirectory(binDirectory);
        _ = Directory.CreateDirectory(overrideBin);
        if (installOmp)
        {
            var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            WriteExecutable(Path.Combine(binDirectory, "omp"), "#!/bin/sh\nexec sh " + Quote(Path.Combine(fixtures, "fake-omp")) + " \"$@\"\n");
        }

        if (uname is not null)
        {
            WriteExecutable(Path.Combine(overrideBin, "uname"), "#!/bin/sh\nprintf '%s\\n' " + Quote(uname) + "\n");
        }
    }
}
