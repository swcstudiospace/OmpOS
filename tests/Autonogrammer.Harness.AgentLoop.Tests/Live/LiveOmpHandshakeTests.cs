using System.Globalization;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.AgentLoop.Launch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Live;

/// <summary>
/// Live proof of Phase 44 criterion 6 and of the engine half of Phase 48 criteria 4 to 7: the real <c>omp</c> started with the
/// planned arguments and environment loads the plugin commands, in an isolated home, without a model turn, and its plugin
/// writes the connection report and the steering heartbeat that make all four plugins read as connected through
/// <see cref="Autonogrammer.Harness.AgentLoop.Connection.PluginConnectionStageSource"/>. The test drives <c>initialize</c> and
/// <c>session/new</c> itself, never sends <c>session/prompt</c>, kills the child tree and checks that no process of the run
/// remains. Excluded from the gate by its trait and skipped unless <c>AUTONOGRAMMER_LIVE_OMP=1</c>, so a plain test run of the project never spawns omp; run it with
/// <c>AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release --no-build -- --filter-trait "Category=Live"</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveOmpHandshakeTests
{
    private const string OptInVariable = "AUTONOGRAMMER_LIVE_OMP";
    private const string SessionId = "live-handshake";
    private const int AuthenticationRequiredCode = -32000;
    private const int PrintedCommandLimit = 40;

    /// <summary>
    /// Measured spawn to <c>session/new</c> result is 17 to 97 s depending on host load (AcpLiveSmokeTests); 300 s still fails a hang.
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(300);

    private static readonly TimeSpan CollectWindow = TimeSpan.FromSeconds(6);

    /// <summary>The omp plugin writes its report at <c>session_start</c>, which precedes the <c>session/new</c> result.</summary>
    private static readonly TimeSpan ReportTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Starts the real omp with the planned launch, lists the commands it announces after <c>session/new</c>, and proves the
    /// child tree is gone after the close.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RealOmpAnnouncesPluginCommandsWithoutAModelTurn()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 to run the live handshake against the installed omp.");
        }

        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The live handshake test finds the omp process tree through /proc.");
        }

        var omp = FindOnPath("omp") ?? SkipWith<string>("omp is not on PATH; the live handshake test is skipped.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var sandbox = new LiveSandbox();
        var plan = LivePluginEvidence.PlanLaunch(sandbox, SessionId);
        sandbox.CreatePlannedDirectories(plan.Environment);

        var marker = "PI_CODING_AGENT_DIR=" + plan.Environment["PI_CODING_AGENT_DIR"];
        var launchEnvironment = new Dictionary<string, string>(plan.Environment, StringComparer.Ordinal) { ["ULTRATHINK_UPLIFT"] = "0" };

        var client = new LiveAcpFrameClient(omp, plan.WorkingDirectory, plan.ExtraArguments, launchEnvironment);
        await using var clientScope = client.ConfigureAwait(true);
        try
        {
            await client.StartAsync(cancellationToken).ConfigureAwait(true);
            await HandshakeAsync(client, plan.WorkingDirectory, cancellationToken).ConfigureAwait(true);
            await LiveAcpFrameClient.CollectAsync(CollectWindow, cancellationToken).ConfigureAwait(true);
            var running = LiveProcessScan.FindByEnvironment(marker);
            var observation = await LivePluginEvidence.ObserveAsync(plan, sandbox, SessionId, ReportTimeout, cancellationToken).ConfigureAwait(true);
            Report(client, running, observation);
            AssertLoaded(client, running, plan);
            LivePluginEvidence.AssertConnected(observation, expectedMode: "rpc");
            running.ShouldContain(
                process => process.Pid == observation.Report.Pid,
                "the process that wrote the connection report is not among the run's omp processes.");
        }
        finally
        {
            await client.CloseAsync(CancellationToken.None).ConfigureAwait(true);
        }

        var remaining = await LiveProcessScan.WaitForNoneAsync(marker, ExitTimeout, cancellationToken).ConfigureAwait(true);
        remaining.Select(process => process.Pid).ShouldBeEmpty("omp processes of the run survived the close.");
    }

    private static async Task HandshakeAsync(LiveAcpFrameClient client, string workspace, CancellationToken cancellationToken)
    {
        var initialize = await client.RequestAsync(
            "initialize",
            new JsonObject
            {
                ["protocolVersion"] = 1,
                ["clientCapabilities"] = new JsonObject
                {
                    ["fs"] = new JsonObject { ["readTextFile"] = false, ["writeTextFile"] = false },
                },
            },
            RequestTimeout,
            cancellationToken).ConfigureAwait(true);
        initialize.IsError.ShouldBeFalse("initialize failed: " + initialize.Describe());

        var created = await NewSessionAsync(client, workspace, cancellationToken).ConfigureAwait(true);
        if (created.IsError && created.ErrorCode == AuthenticationRequiredCode)
        {
            // No credentials exist in the isolated home: authenticate with the agent method, which starts no model turn.
            var authenticated = await client.RequestAsync(
                "authenticate",
                new JsonObject { ["methodId"] = "agent" },
                RequestTimeout,
                cancellationToken).ConfigureAwait(true);
            authenticated.IsError.ShouldBeFalse("authenticate failed: " + authenticated.Describe());
            created = await NewSessionAsync(client, workspace, cancellationToken).ConfigureAwait(true);
        }

        created.IsError.ShouldBeFalse("session/new failed: " + created.Describe() + client.StderrTail());
        var result = created.Result ?? throw new InvalidOperationException("session/new returned no result.");
        result.TryGetProperty("sessionId", out var sessionId).ShouldBeTrue("session/new returned no sessionId.");
        sessionId.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    private static Task<LiveAcpResponse> NewSessionAsync(LiveAcpFrameClient client, string workspace, CancellationToken cancellationToken) =>
        client.RequestAsync(
            "session/new",
            new JsonObject { ["cwd"] = workspace, ["mcpServers"] = new JsonArray() },
            RequestTimeout,
            cancellationToken);

    private static void AssertLoaded(LiveAcpFrameClient client, IReadOnlyList<LiveProcessInfo> running, OmpLaunchPlan plan)
    {
        plan.ExtraArguments[0].ShouldBe("--no-extensions", "the planned arguments must start with --no-extensions.");
        plan.ExtraArguments[1].ShouldBe("-e", "--no-extensions must be followed by -e.");
        plan.ExtraArguments.Contains("--plugin-dir", StringComparer.Ordinal).ShouldBeFalse("the plan must not use --plugin-dir.");
        plan.McpServers.Select(server => server.Name).ShouldBe([OmpLaunchPlanner.SubstrateMcpServerName], "only the substrate MCP server is planned, and the test passes none to session/new.");
        client.SentMethods.Contains("session/prompt", StringComparer.Ordinal).ShouldBeFalse("the live handshake must never send session/prompt.");

        running.ShouldNotBeEmpty("no process carries the run's isolated agent directory, so the run cannot be observed.");
        running.ShouldContain(
            process => process.HasArgumentRun("acp", "--no-extensions", "-e"),
            "no omp process was started as 'acp --no-extensions -e ...'.");
        running.ShouldContain(process => process.HasEnvironmentEntry("ULTRATHINK_UPLIFT=0"), "the child did not get ULTRATHINK_UPLIFT=0.");
        running.ShouldContain(process => process.HasEnvironmentEntry("HOME=" + plan.Environment["HOME"]), "the child did not get the isolated HOME.");

        client.CommandUpdateCount.ShouldBeGreaterThan(0, "omp announced no available_commands_update." + client.StderrTail());
        var commands = client.CommandNames;
        commands.Contains("swarm", StringComparer.Ordinal).ShouldBeTrue("no swarm command was announced.");
        commands.Contains("gsd-core", StringComparer.Ordinal).ShouldBeTrue("no gsd-core command was announced.");
        commands.Any(name => name.StartsWith("ultrathink-", StringComparison.Ordinal)).ShouldBeTrue("no ultrathink-* command was announced.");
    }

    private static void Report(LiveAcpFrameClient client, IReadOnlyList<LiveProcessInfo> running, LivePluginObservation observation)
    {
        var output = TestContext.Current.TestOutputHelper;
        if (output is null)
        {
            return;
        }

        var commands = client.CommandNames;
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"sent: {string.Join(", ", client.SentMethods)}; available_commands_update notifications: {client.CommandUpdateCount}; distinct commands: {commands.Count}"));
        output.WriteLine("commands (first " + PrintedCommandLimit.ToString(CultureInfo.InvariantCulture) + "): " + string.Join(", ", commands.Take(PrintedCommandLimit)));
        output.WriteLine("omp processes of the run: " + string.Join(", ", running.Select(process => process.Pid.ToString(CultureInfo.InvariantCulture))));
        output.WriteLine(observation.Describe());
    }

    private static string? FindOnPath(string executable) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, executable))
            .FirstOrDefault(File.Exists);

    private static T SkipWith<T>(string reason)
    {
        Assert.Skip(reason);
        return default!;
    }
}
