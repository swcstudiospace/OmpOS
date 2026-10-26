using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Live;

/// <summary>
/// Live proof of the wire contract OmpOS relies on for native steering and for the authentication fix, against the installed omp, without
/// a model turn: <c>initialize</c> advertises <c>_meta.steering.supported</c> and the <c>agent</c> authentication method, <c>authenticate</c>
/// with that method lets <c>session/new</c> open a session, <c>_session/steering</c> on the idle session answers <c>promptRequired</c> (omp
/// consumes nothing and starts no turn), and an unknown session id answers the JSON-RPC invalid-params error. The test never sends
/// <c>session/prompt</c> (the frame client refuses it), runs omp in an isolated home with a fake key, and checks that no process of the run
/// remains. Excluded from the gate by its trait and skipped unless <c>OMPOS_LIVE_OMP=1</c>, so a plain test run never spawns omp; run it with
/// <c>OMPOS_LIVE_OMP=1 dotnet test tests/OmpOS.Harness.AgentLoop.Tests -c Release --no-build -- --filter-trait "Category=Live"</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveOmpSteeringContractTests
{
    private const string OptInVariable = "OMPOS_LIVE_OMP";
    private const string SteeringMethod = "_session/steering";
    private const int InvalidParamsCode = -32602;

    /// <summary>Measured spawn to <c>session/new</c> result is 17 to 97 s depending on host load; 300 s still fails a hang.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(300);

    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Runs the whole contract against the real omp in a sandbox.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RealOmpAdvertisesSteeringAuthenticatesAndAnswersAnIdleSteerWithPromptRequired()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 to run the live steering contract against the installed omp.");
        }

        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The live steering contract test finds the omp process tree through /proc.");
        }

        var omp = FindOnPath("omp");
        if (omp is null)
        {
            Assert.Skip("omp is not on PATH; the live steering contract test is skipped.");
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        using var sandbox = new LiveSandbox();
        var home = Path.Combine(sandbox.OmpHome, "home");
        var agentDirectory = Path.Combine(home, ".omp", "agent");
        Directory.CreateDirectory(agentDirectory);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = home,
            ["PI_CODING_AGENT_DIR"] = agentDirectory,
            ["OPENAI_API_KEY"] = "sk-ompos-live-contract-fake-key",
        };
        var marker = "PI_CODING_AGENT_DIR=" + agentDirectory;

        var client = new LiveAcpFrameClient(omp, sandbox.Workspace, [], environment);
        await using var clientScope = client.ConfigureAwait(true);
        try
        {
            await client.StartAsync(cancellationToken).ConfigureAwait(true);
            await AssertContractAsync(client, sandbox.Workspace, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            await client.CloseAsync(CancellationToken.None).ConfigureAwait(true);
        }

        var remaining = await LiveProcessScan.WaitForNoneAsync(marker, ExitTimeout, cancellationToken).ConfigureAwait(true);
        remaining.Select(process => process.Pid).ShouldBeEmpty("omp processes of the run survived the close.");
    }

    private static async Task AssertContractAsync(LiveAcpFrameClient client, string workspace, CancellationToken cancellationToken)
    {
        var initialize = await client.RequestAsync(
            "initialize",
            new JsonObject
            {
                ["protocolVersion"] = 1,
                ["clientCapabilities"] = new JsonObject { ["fs"] = new JsonObject { ["readTextFile"] = false, ["writeTextFile"] = false } },
            },
            RequestTimeout,
            cancellationToken).ConfigureAwait(true);
        initialize.IsError.ShouldBeFalse("initialize failed: " + initialize.Describe());
        var advertised = initialize.Result ?? throw new InvalidOperationException("initialize returned no result.");
        advertised.GetProperty("_meta").GetProperty("steering").GetProperty("supported").ValueKind.ShouldBe(JsonValueKind.True, "omp did not advertise _meta.steering.supported.");
        advertised.GetProperty("authMethods").EnumerateArray().Select(static method => method.GetProperty("id").GetString()).ShouldContain("agent", StringComparer.Ordinal);

        var authenticated = await client.RequestAsync("authenticate", new JsonObject { ["methodId"] = "agent" }, RequestTimeout, cancellationToken).ConfigureAwait(true);
        authenticated.IsError.ShouldBeFalse("authenticate failed: " + authenticated.Describe());

        var created = await client.RequestAsync(
            "session/new",
            new JsonObject { ["cwd"] = workspace, ["mcpServers"] = new JsonArray() },
            RequestTimeout,
            cancellationToken).ConfigureAwait(true);
        created.IsError.ShouldBeFalse("session/new failed: " + created.Describe() + client.StderrTail());
        var sessionId = created.Result?.GetProperty("sessionId").GetString() ?? throw new InvalidOperationException("session/new returned no sessionId.");

        var idle = await client.RequestAsync(SteeringMethod, SteerParameters(sessionId, followUp: false), RequestTimeout, cancellationToken).ConfigureAwait(true);
        idle.IsError.ShouldBeFalse("_session/steering on the idle session failed: " + idle.Describe());
        idle.Result!.Value.GetProperty("outcome").GetString().ShouldBe("promptRequired");
        idle.Result!.Value.GetProperty("queue").GetString().ShouldBe("steering");

        var idleFollowUp = await client.RequestAsync(SteeringMethod, SteerParameters(sessionId, followUp: true), RequestTimeout, cancellationToken).ConfigureAwait(true);
        idleFollowUp.IsError.ShouldBeFalse("_session/steering (followUp) on the idle session failed: " + idleFollowUp.Describe());
        idleFollowUp.Result!.Value.GetProperty("outcome").GetString().ShouldBe("promptRequired");
        idleFollowUp.Result!.Value.GetProperty("queue").GetString().ShouldBe("followUp");

        var unknown = await client.RequestAsync(SteeringMethod, SteerParameters("ompos-no-such-session", followUp: false), RequestTimeout, cancellationToken).ConfigureAwait(true);
        unknown.IsError.ShouldBeTrue("an unknown session id must be an error.");
        unknown.ErrorCode.ShouldBe((long?)InvalidParamsCode);

        client.SentMethods.ShouldNotContain("session/prompt", StringComparer.Ordinal);
    }

    private static JsonObject SteerParameters(string sessionId, bool followUp)
    {
        var parameters = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["prompt"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "live contract probe" }),
        };
        if (followUp)
        {
            parameters["_meta"] = new JsonObject { ["steering"] = new JsonObject { ["queue"] = "followUp" } };
        }

        return parameters;
    }

    private static string? FindOnPath(string executable) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, executable))
            .FirstOrDefault(File.Exists);
}
