using OmpOS.Contracts.Environments;
using OmpOS.Harness.Remote.Containers;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// A "container runtime" that lives in a temporary directory: a per-test <c>podman</c> wrapper script that runs the fixture <c>fake-podman</c>, one container named
/// <see cref="ContainerName"/> with a home directory and an optional fake <c>omp</c> under <c>~/.bun/bin</c>, and a workspace directory (its name holds a space on
/// purpose). The wrapper logs every argument it receives, so a test can assert what was (and was not) on the command line. The files of the "container" are
/// ordinary files in the temporary directory.
/// </summary>
internal sealed class FakeContainerHost : IDisposable
{
    /// <summary>The name of the container every host starts with.</summary>
    public const string ContainerName = "devbox";

    private readonly string _root;

    private FakeContainerHost(string root, FakeContainerHostOptions options)
    {
        _root = root;
        Options = options;
        PodmanDirectory = Path.Combine(root, "podman");
        WorkspaceDirectory = Path.Combine(root, "work space");
        PodmanPath = Path.Combine(root, "podman-wrapper");
        PodmanLogPath = Path.Combine(root, "podman-argv.log");
        OmpLogPath = Path.Combine(root, "omp.log");
    }

    /// <summary>Gets the options the host was created with.</summary>
    public FakeContainerHostOptions Options { get; }

    /// <summary>Gets the wrapper script to use as the podman executable.</summary>
    public string PodmanPath { get; }

    /// <summary>Gets the directory the fake podman keeps its containers and canned listings in.</summary>
    public string PodmanDirectory { get; }

    /// <summary>Gets the home directory of the user in the container.</summary>
    public string HomeDirectory => Path.Combine(PodmanDirectory, "containers", ContainerName, "home");

    /// <summary>Gets the workspace directory inside the "container".</summary>
    public string WorkspaceDirectory { get; }

    /// <summary>Gets the file that receives the podman argument log.</summary>
    public string PodmanLogPath { get; }

    /// <summary>Gets the file that receives what the fake omp saw.</summary>
    public string OmpLogPath { get; }

    /// <summary>Gets the temporary directory that holds the whole fake runtime.</summary>
    public string Root => _root;

    /// <summary>Gets podman client options that point at the wrapper.</summary>
    public PodmanClientOptions ClientOptions => new() { PodmanPath = PodmanPath, CommandTimeout = TimeSpan.FromSeconds(60) };

    /// <summary>Gets a profile for the container.</summary>
    public ContainerProfile Profile => new() { Container = ContainerName, WorkspaceFolder = WorkspaceDirectory };

    /// <summary>Creates the fake runtime.</summary>
    /// <param name="options">How it behaves, or <see langword="null"/> for a healthy runtime with a running container that has omp installed.</param>
    /// <returns>The host. Dispose it to delete its directory.</returns>
    public static FakeContainerHost Create(FakeContainerHostOptions? options = null)
    {
        var effective = options ?? new FakeContainerHostOptions();
        var root = Path.Combine(Path.GetTempPath(), "ompos-container-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(root);
        var host = new FakeContainerHost(root, effective);
        host.Build();
        return host;
    }

    /// <summary>Returns the arguments of every podman call, in order, one list per call.</summary>
    /// <returns>The calls.</returns>
    public IReadOnlyList<IReadOnlyList<string>> PodmanCalls()
    {
        var calls = new List<IReadOnlyList<string>>();
        if (!File.Exists(PodmanLogPath))
        {
            return calls;
        }

        List<string>? current = null;
        foreach (var line in File.ReadAllLines(PodmanLogPath))
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

    /// <summary>Reads the state file of a container.</summary>
    /// <param name="name">The container name.</param>
    /// <returns>The state, for example <c>running</c>.</returns>
    public string StateOf(string name) => File.ReadAllText(Path.Combine(PodmanDirectory, "containers", name, "state")).Trim();

    /// <summary>Writes the canned output of <c>podman ps --format json</c>.</summary>
    /// <param name="json">The JSON text.</param>
    public void SetPsOutput(string json) => File.WriteAllText(Path.Combine(PodmanDirectory, "ps.json"), json);

    /// <summary>Writes the canned output of <c>podman ps --filter ... --format json</c>.</summary>
    /// <param name="json">The JSON text.</param>
    public void SetFilteredPsOutput(string json) => File.WriteAllText(Path.Combine(PodmanDirectory, "ps-filter.json"), json);

    /// <inheritdoc />
    public void Dispose() => FakeGuestFiles.Delete(_root);

    private void Build()
    {
        var container = Path.Combine(PodmanDirectory, "containers", ContainerName);
        _ = Directory.CreateDirectory(Path.Combine(container, "home"));
        _ = Directory.CreateDirectory(WorkspaceDirectory);
        File.WriteAllText(Path.Combine(container, "state"), Options.State + "\n");
        if (Options.Users is { } users)
        {
            File.WriteAllLines(Path.Combine(container, "users"), users);
        }

        var overrideBin = Path.Combine(_root, "override-bin");
        FakeGuestFiles.InstallTools(HomeDirectory, overrideBin, Options.InstallOmp, Options.UnameOutput);

        var q = FakeGuestFiles.Quote;
        var environment = new List<string>
        {
            "FAKE_PODMAN_LOG=" + q(PodmanLogPath),
            "FAKE_OMP_LOG=" + q(OmpLogPath),
            "FAKE_PODMAN_DIR=" + q(PodmanDirectory),
            "FAKE_PODMAN_MODE=" + q(Options.Mode),
            "FAKE_PODMAN_BANNER=" + q(Options.Banner ? "1" : string.Empty),
            "FAKE_PODMAN_START_FAIL=" + q(Options.StartFails ? "1" : string.Empty),
            "FAKE_PODMAN_PATH=" + q(overrideBin + ":/usr/local/bin:/usr/bin:/bin"),
            "FAKE_OMP_VERSION=" + q(Options.OmpVersion),
            "FAKE_OMP_NOISE=" + q(Options.NoiseAfterHandshake ? "1" : string.Empty),
        };
        foreach (var (name, value) in Options.ExtraEnvironment)
        {
            environment.Add(name + "=" + q(value));
        }

        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        FakeGuestFiles.WriteExecutable(PodmanPath, "#!/bin/sh\n" + string.Join(' ', environment) + " exec sh " + q(Path.Combine(fixtures, "fake-podman")) + " \"$@\"\n");
    }
}
