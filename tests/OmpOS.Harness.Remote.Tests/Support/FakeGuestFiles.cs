namespace OmpOS.Harness.Remote.Tests;

/// <summary>File helpers the fake container and WSL hosts share.</summary>
internal static class FakeGuestFiles
{
    /// <summary>Writes a script and makes it executable.</summary>
    /// <param name="path">The file.</param>
    /// <param name="content">The text.</param>
    public static void WriteExecutable(string path, string content)
    {
        File.WriteAllText(path, content);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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
