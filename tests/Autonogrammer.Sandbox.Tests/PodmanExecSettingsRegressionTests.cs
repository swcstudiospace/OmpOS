using Autonogrammer.Sandbox.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Regression tests: container exec applies the request's workdir and environment inside the container,
/// and environment values never travel in the podman argument list.
/// </summary>
public sealed class PodmanExecSettingsRegressionTests
{
    private static readonly string[] ExpectedCommand = ["echo", "hello"];

    /// <summary>
    /// The workdir and environment reach the command inside the container, and the host podman process keeps its own settings.
    /// </summary>
    [Fact]
    public async Task Exec_AppliesWorkdirAndEnvironmentInsideTheContainer()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok("0123456789ab"));
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;
        var provisioned = await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct);

        var request = PodmanTestData.CreateExecRequest() with
        {
            WorkingDirectory = "/workspace",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["GREETING"] = "hello there" },
        };

        var events = new List<ExecEvent>();
        await foreach (var evt in provider.ExecAsync(provisioned.Descriptor.Id, request, ct))
        {
            events.Add(evt);
        }

        events.ShouldContain(e => e.Type == ExecEventType.Exit && e.ExitCode == 0);

        var execSpec = runner.Specs.Last(static s => PodmanTestData.HasArgument(s, "exec"));
        execSpec.WorkingDirectory.ShouldBe("/");
        execSpec.Environment["GREETING"].ShouldBe("hello there");

        var argv = execSpec.Argv.ToList();
        var containerIndex = argv.IndexOf("0123456789ab");
        containerIndex.ShouldBeGreaterThan(0);

        var flags = argv.Take(containerIndex).ToList();
        var workdirIndex = flags.IndexOf("--workdir");
        workdirIndex.ShouldBeGreaterThanOrEqualTo(0);
        flags[workdirIndex + 1].ShouldBe("/workspace");

        var envIndex = flags.IndexOf("--env");
        envIndex.ShouldBeGreaterThanOrEqualTo(0);
        flags[envIndex + 1].ShouldBe("GREETING");

        argv.Skip(containerIndex + 1).ToArray().ShouldBe(ExpectedCommand);
    }

    /// <summary>
    /// A secret value is handed to the child process environment only; no argv element carries the value or a NAME=VALUE pair.
    /// </summary>
    [Fact]
    public async Task Exec_NeverPlacesEnvironmentValuesInArgv()
    {
        const string secretValue = "s3cr3t-value-42";
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok("0123456789ab"));
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;
        var provisioned = await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct);

        var request = PodmanTestData.CreateExecRequest() with
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["SECRET_TOKEN"] = secretValue },
        };

        var events = new List<ExecEvent>();
        await foreach (var evt in provider.ExecAsync(provisioned.Descriptor.Id, request, ct))
        {
            events.Add(evt);
        }

        events.ShouldContain(e => e.Type == ExecEventType.Exit && e.ExitCode == 0);

        var execSpec = runner.Specs.Last(static s => PodmanTestData.HasArgument(s, "exec"));
        execSpec.Argv.Any(arg => arg.Contains(secretValue, StringComparison.Ordinal)).ShouldBeFalse();
        execSpec.Argv.Any(arg => arg.Contains("SECRET_TOKEN=", StringComparison.Ordinal)).ShouldBeFalse();
        execSpec.Argv.ShouldContain("SECRET_TOKEN", StringComparer.Ordinal);
        execSpec.Environment["SECRET_TOKEN"].ShouldBe(secretValue);
    }
}
