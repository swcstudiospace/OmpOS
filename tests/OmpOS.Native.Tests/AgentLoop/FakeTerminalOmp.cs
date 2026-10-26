using System.Text;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// A POSIX shell script that stands in for the omp executable of a terminal session. It answers the version probe, records the
/// arguments, the names (never the values) of its environment variables and its working directory, then either exits with a code or
/// records its process identifier and waits, so only the head can end it. No omp, model or network is involved.
/// </summary>
internal sealed class FakeTerminalOmp
{
    private const UnixFileMode OwnerExecutable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const string Script =
        """
        #!/bin/sh
        if [ "$1" = "version" ]; then echo "omp 1.2.3-fake"; exit 0; fi
        {
          for a in "$@"; do printf 'ARG %s\n' "$a"; done
          env | grep -E '^[^=]+=.' | cut -d= -f1 | sort | sed 's/^/ENV /'
          printf 'PWD %s\n' "$(pwd)"
        } > "__RECORD__.tmp"
        mv "__RECORD__.tmp" "__RECORD__"
        __TAIL__
        """;

    private readonly string _directory;

    private FakeTerminalOmp(string path, string directory)
    {
        Path = path;
        _directory = directory;
    }

    /// <summary>Gets the absolute path of the executable.</summary>
    public string Path { get; }

    /// <summary>Gets the file the process identifier of a waiting fake is written to.</summary>
    public string PidFile => System.IO.Path.Combine(_directory, "omp.pid");

    /// <summary>Gets the file the arguments, environment names and working directory are recorded in. It appears once the fake started.</summary>
    public string RecordFile => System.IO.Path.Combine(_directory, "omp.record");

    /// <summary>Writes a fake that exits with <paramref name="exitCode"/> right after it recorded itself.</summary>
    /// <param name="tree">The tree whose root holds the fake.</param>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>The fake.</returns>
    public static FakeTerminalOmp Exiting(TempTree tree, int exitCode) =>
        Write(tree, $"exit {exitCode}");

    /// <summary>Writes a fake that records its process identifier and waits until it is killed.</summary>
    /// <param name="tree">The tree whose root holds the fake.</param>
    /// <returns>The fake.</returns>
    public static FakeTerminalOmp Waiting(TempTree tree) =>
        Write(tree, "echo $$ > \"__PID__\"\nexec sleep 600");

    /// <summary>Reads the arguments the fake was started with, in order.</summary>
    /// <returns>The arguments.</returns>
    public IReadOnlyList<string> Arguments() => Lines("ARG ");

    /// <summary>Reads the names of the environment variables the fake was started with, in ordinal order.</summary>
    /// <returns>The names.</returns>
    public IReadOnlyList<string> EnvironmentNames() => Lines("ENV ");

    /// <summary>Reads the working directory the fake was started in.</summary>
    /// <returns>The directory.</returns>
    public string WorkingDirectory() => Lines("PWD ").Single();

    /// <summary>Reads the process identifier of a waiting fake.</summary>
    /// <returns>The identifier.</returns>
    public int ProcessId() => int.Parse(File.ReadAllText(PidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Forgets the earlier run, so the next start is recorded from scratch.</summary>
    public void Reset()
    {
        File.Delete(RecordFile);
        File.Delete(PidFile);
    }

    private static FakeTerminalOmp Write(TempTree tree, string tail)
    {
        var directory = System.IO.Path.Combine(tree.Root, "fake-omp");
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, "omp");
        var fake = new FakeTerminalOmp(path, directory);
        var text = Script
            .Replace("__TAIL__", tail, StringComparison.Ordinal)
            .Replace("__RECORD__", fake.RecordFile, StringComparison.Ordinal)
            .Replace("__PID__", fake.PidFile, StringComparison.Ordinal)
            .ReplaceLineEndings("\n");
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, OwnerExecutable);
        }

        return fake;
    }

    private List<string> Lines(string prefix) =>
    [
        .. File.ReadAllLines(RecordFile)
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line[prefix.Length..]),
    ];
}
