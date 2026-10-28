using OmpOS.Contracts.Environments;
using OmpOS.Harness.Remote.Containers;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// A "WSL" that lives in a temporary directory: a per-test <c>wsl.exe</c> wrapper script that runs the fixture <c>fake-wsl</c>, one distribution named
/// <see cref="DistroName"/> with a home directory and an optional fake <c>omp</c>, and a workspace directory (its name holds a space on purpose). The wrapper logs every
/// argument it receives. The files of the "distribution" are ordinary files in the temporary directory. Nothing here needs Windows: the real <c>wsl.exe</c> is never run.
/// </summary>
internal sealed class FakeWslHost : IDisposable
{
    /// <summary>The name of the distribution every host starts with.</summary>
    public const string DistroName = "Ubuntu-24.04";

    private readonly string _root;

    private FakeWslHost(string root, FakeWslHostOptions options)
    {
        _root = root;
        Options = options;
        WslDirectory = Path.Combine(root, "wsl");
        WorkspaceDirectory = Path.Combine(root, "work space");
        WslPath = Path.Combine(root, "wsl-wrapper");
        WslLogPath = Path.Combine(root, "wsl-argv.log");
        OmpLogPath = Path.Combine(root, "omp.log");
    }

    /// <summary>Gets the options the host was created with.</summary>
    public FakeWslHostOptions Options { get; }

    /// <summary>Gets the wrapper script to use as the wsl.exe executable.</summary>
    public string WslPath { get; }

    /// <summary>Gets the directory the fake wsl keeps its distributions in.</summary>
    public string WslDirectory { get; }

    /// <summary>Gets the home directory of the user in the distribution.</summary>
    public string HomeDirectory => Path.Combine(WslDirectory, "distros", DistroName, "home");

    /// <summary>Gets the workspace directory inside the "distribution".</summary>
    public string WorkspaceDirectory { get; }

    /// <summary>Gets the file that receives the wsl argument log.</summary>
    public string WslLogPath { get; }

    /// <summary>Gets the file that receives what the fake omp saw.</summary>
    public string OmpLogPath { get; }

    /// <summary>Gets the temporary directory that holds the whole fake WSL.</summary>
    public string Root => _root;

    /// <summary>Gets wsl.exe client options that point at the wrapper.</summary>
    public WslClientOptions ClientOptions => new() { WslPath = WslPath, CommandTimeout = TimeSpan.FromSeconds(60) };

    /// <summary>Gets a profile for the distribution.</summary>
    public WslProfile Profile => new() { Distro = DistroName, WorkspacePath = WorkspaceDirectory };

    /// <summary>Creates the fake WSL.</summary>
    /// <param name="options">How it behaves, or <see langword="null"/> for a healthy distribution with omp installed.</param>
    /// <returns>The host. Dispose it to delete its directory.</returns>
    public static FakeWslHost Create(FakeWslHostOptions? options = null)
    {
        var effective = options ?? new FakeWslHostOptions();
        var root = Path.Combine(Path.GetTempPath(), "ompos-wsl-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(root);
        var host = new FakeWslHost(root, effective);
        host.Build();
        return host;
    }

    /// <summary>Returns the arguments of every wsl call, in order, one list per call.</summary>
    /// <returns>The calls.</returns>
    public IReadOnlyList<IReadOnlyList<string>> WslCalls()
    {
        var calls = new List<IReadOnlyList<string>>();
        if (!File.Exists(WslLogPath))
        {
            return calls;
        }

        List<string>? current = null;
        foreach (var line in File.ReadAllLines(WslLogPath))
        {
            if (string.Equals(line, "CALL", StringComparison.Ordinal))
            {
                current = [];
                calls.Add(current);
            }
            else if (line.StartsWith("ARG:", StringComparison.Ordinal) && current is not null)
            {
                current.Add(line["ARG:".Length..]);
            }
        }

        return calls;
    }

    /// <summary>Returns the lines the fake omp logged.</summary>
    /// <returns>The lines, or an empty list when it never ran.</returns>
    public IReadOnlyList<string> OmpLog() => File.Exists(OmpLogPath) ? File.ReadAllLines(OmpLogPath) : [];

    /// <inheritdoc />
    public void Dispose() => FakeGuestFiles.Delete(_root);

    private void Build()
    {
        var distro = Path.Combine(WslDirectory, "distros", DistroName);
        _ = Directory.CreateDirectory(Path.Combine(distro, "home"));
        _ = Directory.CreateDirectory(WorkspaceDirectory);
        if (Options.Users is { } users)
        {
            File.WriteAllLines(Path.Combine(distro, "users"), users);
        }

        var overrideBin = Path.Combine(_root, "override-bin");
        FakeGuestFiles.InstallTools(HomeDirectory, overrideBin, Options.InstallOmp, Options.UnameOutput);

        var q = FakeGuestFiles.Quote;
        var environment = new List<string>
        {
            "FAKE_WSL_LOG=" + q(WslLogPath),
            "FAKE_OMP_LOG=" + q(OmpLogPath),
            "FAKE_WSL_DIR=" + q(WslDirectory),
            "FAKE_WSL_BANNER=" + q(Options.Banner ? "1" : string.Empty),
            "FAKE_WSL_PATH=" + q(overrideBin + ":/usr/bin:/bin"),
            "FAKE_OMP_VERSION=" + q(Options.OmpVersion),
        };
        foreach (var (name, value) in Options.ExtraEnvironment)
        {
            environment.Add(name + "=" + q(value));
        }

        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        FakeGuestFiles.WriteExecutable(WslPath, "#!/bin/sh\n" + string.Join(' ', environment) + " exec sh " + q(Path.Combine(fixtures, "fake-wsl")) + " \"$@\"\n");
    }
}
