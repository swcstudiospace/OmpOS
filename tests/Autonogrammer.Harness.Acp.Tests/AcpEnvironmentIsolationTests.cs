using System.Runtime.Versioning;
using System.Text;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// The environment policy of <see cref="AcpStdioTransport"/> and <see cref="OmpAcpClient"/>, checked against a real child process: a POSIX
/// script that writes the environment it received to a file and exits. The parent's variables are set for the duration of each test under names
/// no other test reads.
/// </summary>
public sealed class AcpEnvironmentIsolationTests
{
    private const string Secret = "SECRET_TEST_VAR";
    private const string SecretValue = "parent-secret-value";
    private const string AgentDirectory = "/nonexistent-autonogrammer-agent-dir";
    private const string RecorderBody = "env > \"${0%/*}/environment.txt\"\n";

    /// <summary>Allowlist drops the secret and keeps PATH, HOME, PI_CODING_AGENT_DIR, OMP_*, LC_* and the explicit variables.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task AllowlistDropsSecretsAndKeepsRuntimeAndExplicitVariables()
    {
        SkipUnlessLinux();
        using var parent = new ParentEnvironment();
        parent.Set(Secret, SecretValue);
        parent.Set("PI_CODING_AGENT_DIR", AgentDirectory);
        parent.Set("OMP_ISOLATION_TEST", "omp-value");
        parent.Set("LC_ISOLATION_TEST", "lc-value");
        var explicitVariables = new Dictionary<string, string>(StringComparer.Ordinal) { ["EXPLICIT_ISOLATION_TEST"] = "explicit-value" };

        var seen = await RunAsync(new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist), explicitVariables).ConfigureAwait(true);

        seen.ShouldNotContainKey(Secret);
        seen["PATH"].ShouldBe(Environment.GetEnvironmentVariable("PATH"));
        if (Environment.GetEnvironmentVariable("HOME") is { } home)
        {
            seen["HOME"].ShouldBe(home);
        }

        seen["PI_CODING_AGENT_DIR"].ShouldBe(AgentDirectory);
        seen["OMP_ISOLATION_TEST"].ShouldBe("omp-value");
        seen["LC_ISOLATION_TEST"].ShouldBe("lc-value");
        seen["EXPLICIT_ISOLATION_TEST"].ShouldBe("explicit-value");
    }

    /// <summary>Inherit keeps everything, including the secret, which is the desktop behaviour.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task InheritKeepsEverything()
    {
        SkipUnlessLinux();
        using var parent = new ParentEnvironment();
        parent.Set(Secret, SecretValue);
        parent.Set("OMP_ISOLATION_TEST", "omp-value");

        var seen = await RunAsync(EngineEnvironmentPolicy.Inherit, new Dictionary<string, string>(StringComparer.Ordinal)).ConfigureAwait(true);

        seen[Secret].ShouldBe(SecretValue);
        seen["OMP_ISOLATION_TEST"].ShouldBe("omp-value");
        seen["PATH"].ShouldBe(Environment.GetEnvironmentVariable("PATH"));
    }

    /// <summary>An explicit variable overrides the inherited one of the same name, and an explicit variable the policy would not allow still arrives.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ExplicitVariablesOverrideInheritedOnesAndSurviveTheFilter()
    {
        SkipUnlessLinux();
        using var parent = new ParentEnvironment();
        parent.Set(Secret, SecretValue);
        parent.Set("OMP_ISOLATION_TEST", "parent-omp");
        var explicitVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OMP_ISOLATION_TEST"] = "explicit-omp",
            [Secret] = "explicit-secret",
        };

        var seen = await RunAsync(new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist), explicitVariables).ConfigureAwait(true);

        seen["OMP_ISOLATION_TEST"].ShouldBe("explicit-omp");
        seen[Secret].ShouldBe("explicit-secret");
    }

    /// <summary>Allow-list additions keep their variables, exact and by prefix, while other variables are still dropped.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task AllowListAdditionsAreKept()
    {
        SkipUnlessLinux();
        using var parent = new ParentEnvironment();
        parent.Set(Secret, SecretValue);
        parent.Set("EXTRA_ALLOWED_TEST_VAR", "extra-value");
        parent.Set("EXTRA_PREFIX_TEST_ONE", "prefix-value");
        parent.Set("EXTRA_UNLISTED_TEST_VAR", "unlisted-value");
        var policy = new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist, ["EXTRA_ALLOWED_TEST_VAR", "EXTRA_PREFIX_TEST_*"]);

        var seen = await RunAsync(policy, new Dictionary<string, string>(StringComparer.Ordinal)).ConfigureAwait(true);

        seen["EXTRA_ALLOWED_TEST_VAR"].ShouldBe("extra-value");
        seen["EXTRA_PREFIX_TEST_ONE"].ShouldBe("prefix-value");
        seen.ShouldNotContainKey("EXTRA_UNLISTED_TEST_VAR");
        seen.ShouldNotContainKey(Secret);
    }

    /// <summary>The original constructors keep the whole inherited environment.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task OriginalConstructorInheritsTheWholeEnvironment()
    {
        SkipUnlessLinux();
        using var parent = new ParentEnvironment();
        parent.Set(Secret, SecretValue);
        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "record-environment", RecorderBody);
            var transport = new AcpStdioTransport(script, directory, [], new Dictionary<string, string>(StringComparer.Ordinal));
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);
            await WaitForExitAsync(transport, ct).ConfigureAwait(true);

            (await ReadAsync(directory, ct).ConfigureAwait(true))[Secret].ShouldBe(SecretValue);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>OmpAcpClient builds the policy from the options' EnvironmentMode and EnvironmentAllowList for the process it spawns.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ClientAppliesTheOptionsEnvironmentPolicy()
    {
        SkipUnlessLinux();
        using var parent = new ParentEnvironment();
        parent.Set(Secret, SecretValue);
        parent.Set("EXTRA_ALLOWED_TEST_VAR", "extra-value");
        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "record-environment", RecorderBody);
            var client = new OmpAcpClient();
            var options = new AcpClientOptions
            {
                OmpPath = script,
                WorkingDirectory = directory,
                HandshakeTimeout = TimeSpan.FromSeconds(20),
                EnvironmentMode = EngineEnvironmentMode.Allowlist,
                EnvironmentAllowList = ["EXTRA_ALLOWED_TEST_VAR"],
                EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal) { ["EXPLICIT_ISOLATION_TEST"] = "explicit-value" },
            };
            await using var session = client.CreateSession(options);

            // The recorder exits without speaking ACP, so the handshake fails; the launch is what is under test.
            var failure = await Record.ExceptionAsync(() => session.CreateAsync(ct)).ConfigureAwait(true);

            failure.ShouldBeAssignableTo<HarnessException>();
            var seen = await ReadAsync(directory, ct).ConfigureAwait(true);
            seen.ShouldNotContainKey(Secret);
            seen["EXTRA_ALLOWED_TEST_VAR"].ShouldBe("extra-value");
            seen["EXPLICIT_ISOLATION_TEST"].ShouldBe("explicit-value");
            seen["PATH"].ShouldBe(Environment.GetEnvironmentVariable("PATH"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The options default to inheriting the environment with no allow-list, so existing callers keep their behaviour.</summary>
    [Fact]
    public void OptionsDefaultToInheritingTheEnvironment()
    {
        var options = new AcpClientOptions();

        options.EnvironmentMode.ShouldBe(EngineEnvironmentMode.Inherit);
        options.EnvironmentAllowList.ShouldBeEmpty();
    }

    /// <summary>A null policy is rejected before any process starts.</summary>
    [Fact]
    public void NullPolicyIsRejected()
    {
        string[] noArguments = [];
        var noEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);

        Should.Throw<ArgumentNullException>(() => new AcpStdioTransport("omp", null, noArguments, noEnvironment, null!, null));
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The environment fixture uses a POSIX shell script.");
        }
    }

    [SupportedOSPlatform("linux")]
    private static async Task<Dictionary<string, string>> RunAsync(EngineEnvironmentPolicy policy, IReadOnlyDictionary<string, string> explicitVariables)
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "record-environment", RecorderBody);
            var transport = new AcpStdioTransport(script, directory, [], explicitVariables, policy, null);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);
            await WaitForExitAsync(transport, ct).ConfigureAwait(true);

            return await ReadAsync(directory, ct).ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<Dictionary<string, string>> ReadAsync(string directory, CancellationToken cancellationToken)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in await File.ReadAllLinesAsync(Path.Combine(directory, "environment.txt"), cancellationToken).ConfigureAwait(true))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                seen[line[..separator]] = line[(separator + 1)..];
            }
        }

        return seen;
    }

    private static async Task WaitForExitAsync(AcpStdioTransport transport, CancellationToken cancellationToken)
    {
        var frames = transport.ReceiveAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using var framesScope = frames.ConfigureAwait(true);
        await Should.ThrowAsync<DisconnectedHarnessException>(async () => await frames.MoveNextAsync().ConfigureAwait(true))
            .ConfigureAwait(true);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "acp-environment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [SupportedOSPlatform("linux")]
    private static string WriteScript(string directory, string name, string body)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    /// <summary>Sets variables on the test process and restores the originals when disposed.</summary>
    private sealed class ParentEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originals = new(StringComparer.Ordinal);

        public void Set(string name, string value)
        {
            _ = _originals.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var (name, original) in _originals)
            {
                Environment.SetEnvironmentVariable(name, original);
            }
        }
    }
}
