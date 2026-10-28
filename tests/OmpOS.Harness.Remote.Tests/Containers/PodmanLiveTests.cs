using System.Text;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// Drives the real podman against a throwaway container. Set <c>OMPOS_LIVE_PODMAN=1</c> and <c>OMPOS_LIVE_PODMAN_IMAGE</c> to a fully qualified image that has
/// <c>sh</c>, <c>sed</c>, <c>cat</c>, <c>chmod</c> and <c>mktemp</c> (a small Linux base image) and run the tests of the category Live. The test starts one container with a
/// unique name, puts the fixture fake omp in it (no real omp and no model are involved), and removes the container at the end. Nothing is written outside the container.
/// </summary>
[Trait("Category", "Live")]
public sealed class PodmanLiveTests
{
    private static readonly TimeSpan LiveTimeout = TimeSpan.FromSeconds(120);

    private static string? Variable(string name) => Environment.GetEnvironmentVariable(name);

    private static async Task<SshProcessResult> PodmanAsync(CancellationToken ct, params string[] arguments) =>
        await new SshProcessRunner().RunAsync(new SshInvocation { FileName = "podman", Arguments = arguments }, LiveTimeout, ct).ConfigureAwait(true);

    /// <summary>
    /// The connection test, a real ACP session through <c>podman exec -i</c> with a secret that stays off the command line, the container file system, and the
    /// stopped-container path (diagnosis, then <c>podman start</c>) work against a real container.
    /// </summary>
    [Fact]
    public async Task ARealContainerRunsTheWholePath()
    {
        if (!string.Equals(Variable("OMPOS_LIVE_PODMAN"), "1", StringComparison.Ordinal) || Variable("OMPOS_LIVE_PODMAN_IMAGE") is not { Length: > 0 } image)
        {
            Assert.Skip("Set OMPOS_LIVE_PODMAN=1 and OMPOS_LIVE_PODMAN_IMAGE=<a fully qualified base image> to run the live podman test.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var name = "ompos-live-" + Guid.NewGuid().ToString("N");
        var created = await PodmanAsync(ct, "run", "-d", "--name", name, image, "sleep", "600").ConfigureAwait(true);
        created.ExitCode.ShouldBe(0, created.StandardError);
        try
        {
            var profile = new ContainerProfile { Container = name, WorkspaceFolder = "/tmp", OmpPath = "/usr/local/bin/omp" };
            using (var exec = new PodmanRemoteExec(profile))
            {
                var fakeOmp = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fake-omp"), ct).ConfigureAwait(true);
                var installed = await exec.RunAsync("cat > /usr/local/bin/omp && chmod +x /usr/local/bin/omp", Encoding.UTF8.GetBytes("#!/bin/sh\n" + fakeOmp), 1024, ct).ConfigureAwait(true);
                installed.Succeeded.ShouldBeTrue(installed.Error);
            }

            var report = await new ContainerPreflight().RunAsync(profile, ct).ConfigureAwait(true);
            report.Diagnoses.Where(diagnosis => diagnosis.Severity == DiagnosisSeverity.Error).ShouldBeEmpty();
            report.Succeeded.ShouldBeTrue();

            await AssertSessionAsync(profile, ct).ConfigureAwait(true);
            await AssertFilesAsync(profile, ct).ConfigureAwait(true);

            _ = await PodmanAsync(ct, "stop", "-t", "1", name).ConfigureAwait(true);
            var stopped = await new ContainerPreflight().RunAsync(profile, ct).ConfigureAwait(true);
            stopped.Diagnoses.ShouldHaveSingleItem().Kind.ShouldBe(RemoteDiagnosisKind.ContainerNotRunning);
            var started = await new ContainerPreflight().StartAsync(profile, ct).ConfigureAwait(true);
            started.Started.ShouldBeTrue();
            (await new ContainerPreflight().RunAsync(profile, ct).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        }
        finally
        {
            _ = await PodmanAsync(CancellationToken.None, "rm", "-f", name).ConfigureAwait(true);
        }
    }

    private static async Task AssertSessionAsync(ContainerProfile profile, CancellationToken ct)
    {
        const string Secret = "live-canary-4c9f2b7e";
        var launch = new RemoteLaunch
        {
            WorkingDirectory = "/tmp",
            OmpPath = "/usr/local/bin/omp",
            Arguments = [],
            StateDirectory = "/tmp/ompos-live-state",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["OMPOS_TEST_SECRET"] = Secret },
            SecretEnvironmentNames = new HashSet<string>(StringComparer.Ordinal) { "OMPOS_TEST_SECRET" },
            Files = [],
            ReverseForwards = [],
            McpServers = [],
            PlanningEvents = [],
        };
        var factory = PodmanAcpTransportFactory.Create(profile, launch, new PodmanExecCommandBuilder());
        var session = new OmpAcpClient(factory).CreateSession(new AcpClientOptions { WorkingDirectory = "/tmp", HandshakeTimeout = LiveTimeout, RequestTimeout = LiveTimeout });
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);
        var events = new List<HarnessEvent>();
        await foreach (var evt in session.SubmitPromptAsync("hello", new PromptOptions(), ct).ConfigureAwait(true))
        {
            events.Add(evt);
        }

        events.OfType<TextDeltaEvent>().Single().Text.ShouldBe($"echo:hello|secretlen={Secret.Length}");
    }

    private static async Task AssertFilesAsync(ContainerProfile profile, CancellationToken ct)
    {
        using var exec = new PodmanRemoteExec(profile);
        var fileSystem = new RemoteWorkspaceFileSystem(exec);

        await fileSystem.CreateFileAsync("ompos-live-file.txt", "made in the container", ct).ConfigureAwait(true);
        var text = await fileSystem.ReadFileAsync("ompos-live-file.txt", 1024, ct).ConfigureAwait(true);
        await fileSystem.DeleteFileAsync("ompos-live-file.txt", text, ct).ConfigureAwait(true);

        text.ShouldBe("made in the container");
    }
}
