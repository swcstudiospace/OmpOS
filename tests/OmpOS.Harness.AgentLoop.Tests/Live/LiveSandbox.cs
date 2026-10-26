using System.Globalization;

namespace OmpOS.Harness.AgentLoop.Tests.Live;

/// <summary>
/// A temporary directory for one live run: the isolated omp home, the session state root and an empty workspace. Nothing
/// under the real home is read or written by a run that is launched inside it. It is deleted when disposed.
/// </summary>
internal sealed class LiveSandbox : IDisposable
{
    /// <summary>Creates the sandbox with its home, state root and workspace directories.</summary>
    public LiveSandbox()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "ompos-live-handshake",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(OmpHome);
        Directory.CreateDirectory(Path.Combine(OmpHome, "home"));
        Directory.CreateDirectory(Path.Combine(OmpHome, "agent"));
        Directory.CreateDirectory(StateRoot);
        Directory.CreateDirectory(Workspace);
    }

    /// <summary>Gets the sandbox root.</summary>
    public string Root { get; }

    /// <summary>Gets the directory that replaces the user's home (<c>home</c> and <c>agent</c> live below it).</summary>
    public string OmpHome => Path.Combine(Root, "omp");

    /// <summary>Gets the root of the session state.</summary>
    public string StateRoot => Path.Combine(Root, "state");

    /// <summary>Gets the empty workspace the session is opened in. It is outside every repository, so no project config applies.</summary>
    public string Workspace => Path.Combine(Root, "workspace");

    /// <summary>Finds the repository root by looking for <c>OmpOS.slnx</c> above the test output directory.</summary>
    /// <returns>The absolute repository root.</returns>
    /// <exception cref="DirectoryNotFoundException">The solution file was not found.</exception>
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("OmpOS.slnx was not found above the test output directory.");
    }

    /// <summary>
    /// Creates every planned environment value that is a directory path inside the sandbox, mode 0700 like the head does for a
    /// session. A variable whose name ends in <c>_FILE</c> names a file (for example <c>OMPOS_CONNECTION_FILE</c>), so only its
    /// parent directory is created: the omp plugin never creates that parent and would not write its report without it.
    /// </summary>
    /// <param name="environment">The planned environment.</param>
    public void CreatePlannedDirectories(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var prefix = Root + Path.DirectorySeparatorChar;
        foreach (var (name, value) in environment)
        {
            if (!Path.IsPathRooted(value) || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var directory = name.EndsWith("_FILE", StringComparison.Ordinal) ? Path.GetDirectoryName(value) : value;
            if (!string.IsNullOrEmpty(directory))
            {
                CreatePrivateDirectory(directory);
            }
        }
    }

    private static void CreatePrivateDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temp directory must not fail the proof.
        }
    }
}
