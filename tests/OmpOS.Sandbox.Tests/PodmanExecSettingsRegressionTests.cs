using OmpOS.Sandbox.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Regression tests: container exec applies the request's workdir and environment inside the container.
/// </summary>
public sealed class PodmanExecSettingsRegressionTests
{
    /// <summary>
    /// The workdir and environment reach the command inside the container, and the host podman process keeps its own settings.
    /// </summary>
    [Fact]
    public async Task Exec_AppliesWorkdirAndEnvironmentInsideTheContainer()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok("container-id-123"));
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
        execSpec.Environment.ShouldBeEmpty();

        var argv = execSpec.Argv.ToList();
        var containerIndex = argv.IndexOf("container-id-123");
        containerIndex.ShouldBeGreaterThan(0);

        var flags = argv.Take(containerIndex).ToList();
        var workdirIndex = flags.IndexOf("--workdir");
        workdirIndex.ShouldBeGreaterThanOrEqualTo(0);
        flags[workdirIndex + 1].ShouldBe("/workspace");

        var envIndex = flags.IndexOf("--env");
        envIndex.ShouldBeGreaterThanOrEqualTo(0);
        flags[envIndex + 1].ShouldBe("GREETING=hello there");

        argv.Skip(containerIndex + 1).ToArray().ShouldBe(new[] { "echo", "hello" });
    }
}
