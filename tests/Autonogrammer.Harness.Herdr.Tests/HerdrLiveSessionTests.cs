using System.Security.Cryptography;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

/// <summary>
/// Drives the real herdr executable through <see cref="HerdrCliSessionClient"/>. Set <c>AUTONOGRAMMER_LIVE_HERDR=1</c> to run it.
/// The executable is the path in <c>HERDR_BIN</c>, or else the first <c>herdr</c> on <c>PATH</c>. The test creates a workspace
/// labelled <c>autonogrammer-live-</c> followed by eight random hex digits, starts one omp agent in it, reads the agent, confirms
/// it is listed, then terminates the agent and closes the workspace. Cleanup runs in a finally block, so it happens even when an
/// assertion fails. No argument carries a secret or a home path.
/// </summary>
[Trait("Category", "Live")]
public sealed class HerdrLiveSessionTests
{
    private const string EnableVariable = "AUTONOGRAMMER_LIVE_HERDR";
    private const string BinaryVariable = "HERDR_BIN";
    private const string LabelPrefix = "autonogrammer-live-";
    private const string WorkspaceNotFoundCode = "workspace_not_found";
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    /// <summary>Starts one omp agent in a fresh workspace, reads and lists it, then removes the agent and its workspace.</summary>
    [Fact]
    public async Task AnOmpAgentIsStartedReadListedAndRemoved()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {EnableVariable}=1 to run the live herdr test.");
            return;
        }

        string? executable = ResolveExecutable();
        if (executable is null)
        {
            Assert.Skip($"herdr was not found. Set {BinaryVariable} to the herdr executable, or put herdr on PATH.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var runner = new HerdrSystemProcessRunner();
        var client = new HerdrCliSessionClient(runner, executable, HeartbeatInterval);
        var id = new HerdrSessionId(LabelPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4)));
        var created = false;
        try
        {
            await client.CreateAsync(new HerdrCreateRequest(id, "omp", null), ct).ConfigureAwait(true);
            created = true;

            HerdrSessionSnapshot status = await client.StatusAsync(id, ct).ConfigureAwait(true);
            status.Id.Value.ShouldBe(id.Value);
            status.State.ShouldBe(HerdrSessionState.Running);

            IReadOnlyList<HerdrSessionSnapshot> listed = await client.ListAsync(ct).ConfigureAwait(true);
            listed.Any(session => string.Equals(session.Id.Value, id.Value, StringComparison.Ordinal)).ShouldBeTrue();
        }
        finally
        {
            if (created)
            {
                await CleanUpAsync(client, runner, executable, id).ConfigureAwait(true);
            }
        }
    }

    /// <summary>
    /// Ends the agent and closes its workspace. It runs without the test's token, so a cancelled test still cleans up. The
    /// workspace is read while the agent still exists. The workspace is closed even when the termination fails.
    /// </summary>
    private static async Task CleanUpAsync(HerdrCliSessionClient client, HerdrSystemProcessRunner runner, string executable, HerdrSessionId id)
    {
        string? workspaceId = null;
        try
        {
            workspaceId = await ReadWorkspaceIdAsync(runner, executable, id.Value, CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            try
            {
                await client.TerminateAsync(id, CancellationToken.None).ConfigureAwait(true);
            }
            finally
            {
                if (workspaceId is not null)
                {
                    await CloseWorkspaceAsync(runner, executable, workspaceId).ConfigureAwait(true);
                }
            }
        }
    }

    /// <summary>Reads the workspace of a named agent with <c>agent get</c>, since a session snapshot does not carry it.</summary>
    private static async Task<string> ReadWorkspaceIdAsync(HerdrSystemProcessRunner runner, string executable, string agentName, CancellationToken cancellationToken)
    {
        HerdrProcessResult result = await runner.RunAsync(executable, ["agent", "get", agentName], cancellationToken).ConfigureAwait(true);
        result.ExitCode.ShouldBe(0, result.StandardError);

        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        JsonElement agent = document.RootElement.GetProperty("result").GetProperty("agent");
        return agent.GetProperty("workspace_id").GetString()
            ?? throw new InvalidOperationException("herdr agent get did not return a workspace_id.");
    }

    private static async Task CloseWorkspaceAsync(HerdrSystemProcessRunner runner, string executable, string workspaceId)
    {
        HerdrProcessResult result = await runner.RunAsync(executable, ["workspace", "close", workspaceId], CancellationToken.None).ConfigureAwait(true);

        // herdr closes a workspace with its last pane, so the workspace may already be gone when cleanup runs.
        if (result.ExitCode == 1 && string.Equals(ErrorCode(result.StandardError), WorkspaceNotFoundCode, StringComparison.Ordinal))
        {
            return;
        }

        result.ExitCode.ShouldBe(0, result.StandardError);
    }

    /// <summary>Returns the error code of the envelope in the text, or null when the text holds no error envelope.</summary>
    private static string? ErrorCode(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out JsonElement error) || error.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return error.TryGetProperty("code", out JsonElement code) && code.ValueKind == JsonValueKind.String ? code.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Returns the herdr executable: the <c>HERDR_BIN</c> path when it is set, otherwise the first herdr on <c>PATH</c>, or null.</summary>
    private static string? ResolveExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable(BinaryVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        string fileName = OperatingSystem.IsWindows() ? "herdr.exe" : "herdr";
        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
