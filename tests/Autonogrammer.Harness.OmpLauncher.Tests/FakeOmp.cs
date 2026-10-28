namespace Autonogrammer.Harness.OmpLauncher.Tests;

/// <summary>
/// A temporary directory tree holding fake omp executables. Each fake is a small shell script written by the test, so no
/// real omp, model or session is ever started. Every fake is written under <see cref="Root"/> and nowhere else.
/// </summary>
internal sealed class FakeOmp : IDisposable
{
    private const UnixFileMode OwnerExecutable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public FakeOmp()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The fake omp executables are POSIX shell scripts.");
        }

        Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "autonogrammer-launcher-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
    }

    /// <summary>The root of the temporary tree.</summary>
    public string Root { get; }

    /// <summary>Creates a directory under the root and returns its full path.</summary>
    public string CreateDirectory(string name) => Directory.CreateDirectory(Path.Combine(Root, name)).FullName;

    /// <summary>Writes an executable shell script named <paramref name="fileName"/> into <paramref name="directory"/>.</summary>
    public string WriteExecutable(string directory, string body, string fileName = "omp")
    {
        var path = FileUnderRoot(directory, fileName);
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        SetMode(path, OwnerExecutable);
        return path;
    }

    /// <summary>Writes a file named <paramref name="fileName"/> into <paramref name="directory"/> without the execute bit.</summary>
    public string WriteNonExecutable(string directory, string fileName = "omp")
    {
        var path = FileUnderRoot(directory, fileName);
        File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
        SetMode(path, OwnerReadWrite);
        return path;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private string FileUnderRoot(string directory, string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Fake executables must be written under {Root}: {path}");
        }

        return path;
    }

    private static void SetMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The fake omp executables are POSIX shell scripts.");
        }

        File.SetUnixFileMode(path, mode);
    }
}
