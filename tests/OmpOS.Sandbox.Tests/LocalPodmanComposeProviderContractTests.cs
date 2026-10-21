using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmpOS.Sandbox.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Contract tests for LocalPodmanComposeProvider (with fake runner).
/// </summary>
public sealed class LocalPodmanComposeProviderContractTests : SandboxProviderContractTests<LocalPodmanComposeProvider>
{
    private FakeProcessRunner _fakeRunner = null!;

    /// <inheritdoc />
    protected override LocalPodmanComposeProvider CreateProvider()
    {
        _fakeRunner = new FakeProcessRunner();
        _fakeRunner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "0123456789ab",
            Stderr = string.Empty,
            TimedOut = false,
        });

        var options = Options.Create(new SandboxProviderOptions());
        var logger = NullLogger<LocalPodmanComposeProvider>.Instance;

        return new LocalPodmanComposeProvider(_fakeRunner, logger, options);
    }

    /// <inheritdoc />
    protected override void DisposeProvider(LocalPodmanComposeProvider provider)
    {
    }

    [Fact]
    public async Task Provision_GeneratesComposeFileAndRunsUp()
    {
        var provider = CreateProvider();
        try
        {
            var request = CreateProvisionRequest();

            var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

            result.NewlyProvisioned.ShouldBeTrue();
            var upSpec = _fakeRunner.Specs.FirstOrDefault(static s => s.Argv.Any(static a => string.Equals(a, "up", StringComparison.Ordinal)));
            upSpec.ShouldNotBeNull();
            upSpec!.Argv.ShouldContain(static a => string.Equals(a, "podman-compose", StringComparison.Ordinal));
            upSpec.Argv.ShouldContain(static a => string.Equals(a, "-p", StringComparison.Ordinal));
            upSpec.Argv.ShouldContain(static a => string.Equals(a, "-d", StringComparison.Ordinal));
            upSpec.Argv.ShouldContain(static a => string.Equals(a, "--wait", StringComparison.Ordinal));
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task Exec_RunsPodmanExec()
    {
        var provider = CreateProvider();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            var request = CreateProvisionRequest();
            var result = await provider.ProvisionAsync(request, ct);

            _fakeRunner.SetResult(new ProcessResult
            {
                ExitCode = 0,
                Stdout = "hello\n",
                Stderr = string.Empty,
                TimedOut = false,
            });

            var events = new List<ExecEvent>();
            await foreach (var evt in provider.ExecAsync(result.Descriptor.Id, CreateExecRequest(), ct))
            {
                events.Add(evt);
            }

            events.ShouldContain(e => e.Type == ExecEventType.Stdout && e.Stdout!.Contains("hello", StringComparison.Ordinal));
            events.ShouldContain(e => e.Type == ExecEventType.Exit && e.ExitCode == 0);

            var execSpec = _fakeRunner.Specs.FirstOrDefault(static s => s.Argv.Any(static a => string.Equals(a, "exec", StringComparison.Ordinal)));
            execSpec.ShouldNotBeNull();
            execSpec!.Argv.ShouldContain(static a => string.Equals(a, "podman", StringComparison.Ordinal));
            execSpec.Argv.ShouldContain(static a => string.Equals(a, "exec", StringComparison.Ordinal));
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task Teardown_RunsPodmanComposeDown()
    {
        var provider = CreateProvider();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            var request = CreateProvisionRequest();
            var result = await provider.ProvisionAsync(request, ct);

            _fakeRunner.SetResult(new ProcessResult
            {
                ExitCode = 0,
                Stdout = string.Empty,
                Stderr = string.Empty,
                TimedOut = false,
            });

            await provider.TeardownAsync(result.Descriptor.Id, ct);

            var downSpec = _fakeRunner.Specs.FirstOrDefault(static s => s.Argv.Any(static a => string.Equals(a, "down", StringComparison.Ordinal)));
            downSpec.ShouldNotBeNull();
            downSpec!.Argv.ShouldContain(static a => string.Equals(a, "podman-compose", StringComparison.Ordinal));
            downSpec.Argv.ShouldContain(static a => string.Equals(a, "down", StringComparison.Ordinal));
            downSpec.Argv.ShouldContain(static a => string.Equals(a, "-v", StringComparison.Ordinal));
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task RealPodmanComposeConfig_SmokeTest()
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(
            await CommandProbe.CheckCommandExistsAsync("podman", ct)
                && await CommandProbe.CheckCommandExistsAsync("podman-compose", ct),
            "podman or podman-compose not available");

        // Config-only smoke: render the repo compose file. Never starts containers
        // (no ProvisionAsync/TeardownAsync), so this cannot leave anything running.
        var composePath = FindRepoComposeFile();
        var runner = new ProcessRunner();
        var result = await runner.RunAsync(new ProcessSpec
        {
            Argv = ["podman-compose", "-f", composePath, "--profile", "workflows", "config"],
            WorkingDirectory = Path.GetDirectoryName(composePath)!,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = TimeSpan.FromSeconds(60),
        }, ct);

        result.TimedOut.ShouldBeFalse();
        result.ExitCode.ShouldBe(0);
        result.Stdout.ShouldContain("temporal");
    }

    private static string FindRepoComposeFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OmpOS.slnx")))
            {
                var composePath = Path.Combine(dir.FullName, "deploy", "compose.yaml");
                if (!File.Exists(composePath))
                {
                    throw new InvalidOperationException($"deploy/compose.yaml not found under {dir.FullName}");
                }

                return composePath;
            }
        }

        throw new InvalidOperationException($"Could not locate OmpOS.slnx walking up from {AppContext.BaseDirectory}");
    }
}
