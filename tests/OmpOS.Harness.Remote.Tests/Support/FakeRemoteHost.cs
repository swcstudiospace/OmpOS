using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// A "remote machine" that lives in a temporary directory: a per-test <c>ssh</c> wrapper script that runs the fixture <c>fake-ssh</c>, a remote
/// home directory, an optional fake <c>omp</c> under <c>~/.bun/bin</c>, and a workspace directory. The ssh wrapper logs every argument it receives,
/// so a test can assert what was (and was not) on the command line.
/// </summary>
internal sealed class FakeRemoteHost : IDisposable
{
    private readonly string _root;

    private FakeRemoteHost(string root, FakeRemoteHostOptions options)
    {
        _root = root;
        Options = options;
        HomeDirectory = Path.Combine(root, "home");
        WorkspaceDirectory = Path.Combine(root, "work space");
        SshLogPath = Path.Combine(root, "ssh-argv.log");
        OmpLogPath = Path.Combine(root, "omp.log");
        SshPath = Path.Combine(root, "ssh");
    }

    /// <summary>Gets the options the host was created with.</summary>
    public FakeRemoteHostOptions Options { get; }

    /// <summary>Gets the wrapper script to use as the ssh executable.</summary>
    public string SshPath { get; }

    /// <summary>Gets the home directory of the remote user.</summary>
    public string HomeDirectory { get; }

    /// <summary>Gets the remote workspace directory (its name holds a space on purpose).</summary>
    public string WorkspaceDirectory { get; }

    /// <summary>Gets the file that receives the ssh argument log.</summary>
    public string SshLogPath { get; }

    /// <summary>Gets the file that receives what the fake omp saw.</summary>
    public string OmpLogPath { get; }

    /// <summary>Gets the temporary directory that holds the whole fake machine.</summary>
    public string Root => _root;

    /// <summary>Gets a ssh client configuration that points at the wrapper.</summary>
    public SshClientOptions ClientOptions => new() { SshPath = SshPath, CommandTimeout = TimeSpan.FromSeconds(60) };

    /// <summary>Gets a profile for the fake machine.</summary>
    public SshProfile Profile => new() { Alias = "fake-host", RemoteWorkspaceRoot = WorkspaceDirectory };

    /// <summary>Creates the fake machine.</summary>
    /// <param name="options">How it behaves, or <see langword="null"/> for a healthy machine with omp installed.</param>
    /// <returns>The machine. Dispose it to delete its directory.</returns>
    public static FakeRemoteHost Create(FakeRemoteHostOptions? options = null)
    {
        var effective = options ?? new FakeRemoteHostOptions();
        var root = Path.Combine(Path.GetTempPath(), "ompos-remote-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(root);
        var host = new FakeRemoteHost(root, effective);
        host.Build();
        return host;
    }

    /// <summary>Returns the arguments of every ssh call, in order, one list per call.</summary>
    /// <returns>The calls.</returns>
    public IReadOnlyList<IReadOnlyList<string>> SshCalls()
    {
        var calls = new List<IReadOnlyList<string>>();
        if (!File.Exists(SshLogPath))
        {
            return calls;
        }

        List<string>? current = null;
        foreach (var line in File.ReadAllLines(SshLogPath))
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
    public IReadOnlyList<string> OmpLog() =>
        File.Exists(OmpLogPath) ? File.ReadAllLines(OmpLogPath) : [];

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory does not fail a test.
        }
    }

    private static string Quote(string value) => PosixQuoting.Quote(value);

    private void Build()
    {
        var binDirectory = Path.Combine(HomeDirectory, ".bun", "bin");
        _ = Directory.CreateDirectory(binDirectory);
        _ = Directory.CreateDirectory(WorkspaceDirectory);
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        if (Options.InstallOmp)
        {
            FakeGuestFiles.WriteExecutable(Path.Combine(binDirectory, "omp"), "#!/bin/sh\nexec sh " + Quote(Path.Combine(fixtures, "fake-omp")) + " \"$@\"\n");
        }

        var overrideBin = Path.Combine(_root, "override-bin");
        _ = Directory.CreateDirectory(overrideBin);
        if (Options.UnameOutput is { } uname)
        {
            FakeGuestFiles.WriteExecutable(Path.Combine(overrideBin, "uname"), "#!/bin/sh\nprintf '%s\\n' " + Quote(uname) + "\n");
        }

        var gDirectory = Path.Combine(_root, "ssh-g");
        _ = Directory.CreateDirectory(gDirectory);
        foreach (var (alias, content) in Options.CannedConfigs)
        {
            File.WriteAllText(Path.Combine(gDirectory, alias + ".txt"), content);
        }

        var environment = new List<string>
        {
            "FAKE_SSH_LOG=" + Quote(SshLogPath),
            "FAKE_OMP_LOG=" + Quote(OmpLogPath),
            "FAKE_SSH_G_DIR=" + Quote(gDirectory),
            "FAKE_SSH_MODE=" + Quote(Options.Mode),
            "FAKE_SSH_BANNER=" + Quote(Options.Banner ? "1" : string.Empty),
            "FAKE_SSH_HOME=" + Quote(HomeDirectory),
            "FAKE_SSH_PATH=" + Quote(overrideBin + ":/usr/bin:/bin"),
            "FAKE_OMP_VERSION=" + Quote(Options.OmpVersion),
            "FAKE_OMP_NOISE=" + Quote(Options.NoiseAfterHandshake ? "1" : string.Empty),
        };
        foreach (var (name, value) in Options.ExtraEnvironment)
        {
            environment.Add(name + "=" + Quote(value));
        }

        var script = "#!/bin/sh\n" + string.Join(' ', environment) + " exec sh " + Quote(Path.Combine(fixtures, "fake-ssh")) + " \"$@\"\n";
        FakeGuestFiles.WriteExecutable(SshPath, script);
    }
}
