using OmpOS.Contracts.Environments;
using OmpOS.Harness.Acp;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// Drives the real ssh client against a real sshd and a real <c>omp acp</c>. Start the server with <c>scripts/remote/loopback-sshd.sh</c>, export the
/// variables of its <c>OMPOS_LIVE_ENV</c> line (<c>OMPOS_LIVE_SSH=1</c>, <c>OMPOS_LIVE_SSH_PORT</c>, <c>OMPOS_LIVE_SSH_KEY</c>, <c>OMPOS_LIVE_SSH_KNOWN_HOSTS</c>,
/// <c>OMPOS_LIVE_SSH_USER</c>) and run the tests of the category Live. The omp session runs with an isolated home directory and sends no prompt, so no
/// model is contacted and nothing is written to the real omp state.
/// </summary>
[Trait("Category", "Live")]
public sealed class RemoteSshLiveTests
{
    /// <summary>Bound for the live handshake: omp takes tens of seconds to answer <c>initialize</c> on a loaded host.</summary>
    private static readonly TimeSpan LiveTimeout = TimeSpan.FromSeconds(300);

    private static string? Variable(string name) => Environment.GetEnvironmentVariable(name);

    private static string? FindOnPath(string name)
    {
        foreach (var directory in (Variable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static SshProfile? LiveProfile(string workspace, string ompWrapper)
    {
        if (!string.Equals(Variable("OMPOS_LIVE_SSH"), "1", StringComparison.Ordinal)
            || !int.TryParse(Variable("OMPOS_LIVE_SSH_PORT"), out var port)
            || Variable("OMPOS_LIVE_SSH_KEY") is not { Length: > 0 } key
            || Variable("OMPOS_LIVE_SSH_KNOWN_HOSTS") is not { Length: > 0 } knownHosts)
        {
            return null;
        }

        return new SshProfile
        {
            Alias = "live-loopback",
            Hostname = "127.0.0.1",
            Port = port,
            User = Variable("OMPOS_LIVE_SSH_USER") ?? Environment.UserName,
            IdentityFile = key,
            Options = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UserKnownHostsFile"] = knownHosts,
                ["StrictHostKeyChecking"] = "yes",
            },
            RemoteWorkspaceRoot = workspace,
            RemoteOmpPath = ompWrapper,
        };
    }

    /// <summary>
    /// Writes a wrapper that gives omp the local PATH (the non-interactive PATH of sshd would not find Bun) and uses it as the remote omp, so the
    /// live test needs no installation on the server side.
    /// </summary>
    private static string WriteOmpWrapper(TempDirectory directory)
    {
        var omp = Variable("OMPOS_LIVE_SSH_OMP") ?? FindOnPath("omp") ?? throw new InvalidOperationException("omp is not on PATH and OMPOS_LIVE_SSH_OMP is not set.");
        var wrapper = directory.Combine("omp-live");
        File.WriteAllText(wrapper, "#!/bin/sh\nPATH=" + PosixQuoting.Quote(Variable("PATH") ?? string.Empty) + "\nexport PATH\nexec " + PosixQuoting.Quote(omp) + " \"$@\"\n");
        File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return wrapper;
    }

    /// <summary>The connection test passes against the loopback server, and a real <c>omp acp</c> opens a session through the tunnel with an isolated home.</summary>
    [Fact]
    public async Task ARealOmpOpensASessionThroughARealSshd()
    {
        if (!string.Equals(Variable("OMPOS_LIVE_SSH"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set OMPOS_LIVE_SSH=1 and the OMPOS_LIVE_SSH_* variables printed by scripts/remote/loopback-sshd.sh to run the live ssh test.");
        }

        var ct = TestContext.Current.CancellationToken;
        using var directory = TempDirectory.Create();
        var wrapper = WriteOmpWrapper(directory);
        var workspace = directory.Combine("workspace");
        _ = Directory.CreateDirectory(workspace);
        if (LiveProfile(workspace, wrapper) is not { } profile)
        {
            Assert.Skip("Set OMPOS_LIVE_SSH=1 and the OMPOS_LIVE_SSH_* variables printed by scripts/remote/loopback-sshd.sh to run the live ssh test.");
            return;
        }

        var report = await new RemotePreflight().RunAsync(profile, ct).ConfigureAwait(true);
        report.Diagnoses.Where(diagnosis => diagnosis.Severity == DiagnosisSeverity.Error).ShouldBeEmpty();
        report.Succeeded.ShouldBeTrue();

        var home = directory.Combine("omp-home");
        var launch = new RemoteLaunch
        {
            WorkingDirectory = workspace,
            OmpPath = wrapper,
            Arguments = ["--no-extensions"],
            StateDirectory = directory.Combine("state"),
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOME"] = home,
                ["PI_CODING_AGENT_DIR"] = Path.Combine(home, ".omp", "agent"),
            },
            SecretEnvironmentNames = new HashSet<string>(StringComparer.Ordinal),
            Files = [],
            ReverseForwards = [],
            McpServers = [],
            PlanningEvents = [],
        };
        _ = Directory.CreateDirectory(home);
        var factory = SshAcpTransportFactory.Create(profile, launch, new SshCommandBuilder());
        var session = new OmpAcpClient(factory).CreateSession(new AcpClientOptions
        {
            WorkingDirectory = workspace,
            AuthMethodId = AcpClientOptions.OmpAuthMethodId,
            HandshakeTimeout = LiveTimeout,
            RequestTimeout = LiveTimeout,
        });
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.AdvertisedCapabilities["loadSession"].ShouldBe("true");
    }
}
