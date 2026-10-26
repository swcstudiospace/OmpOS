using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>MCP server specs on <c>session/new</c> and <c>session/load</c>: exact wire JSON, validation, and secret redaction.</summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
public sealed class AcpMcpServerSessionTests
{
    private const string HeaderSecret = "Bearer header-secret-7f3a";
    private const string EnvSecretA = "env-secret-alpha-91c2";
    private const string EnvSecretZ = "env-secret-zeta-44d8";
    private const string ArgSecret = "arg-secret-b05e";
    private const string Cwd = AcpFixtures.CapturedCwd;

    private static readonly string[] AllSecrets = [HeaderSecret, EnvSecretA, EnvSecretZ, ArgSecret];

    /// <summary>With no MCP servers configured the frames are byte-identical to the captured ones: <c>mcpServers</c> is an empty array.</summary>
    [Fact]
    public async Task DefaultOptionsSendAnEmptyMcpServerList()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = new AcpAgentSession(transport, Options(servers: []));
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);

        ParamsOf(transport, "session/new").ToJsonString().ShouldBe($"{{\"cwd\":\"{Cwd}\",\"mcpServers\":[]}}");
        AcpFixtures.Text(transport.Sent.Single(static frame => HasMethod(frame, "session/new")))
            .ShouldBe(AcpFixtures.Lines("acp-in.ndjson")[2]);
    }

    /// <summary>A stdio spec has command, args and env as name/value pairs in ordinal name order, and no type.</summary>
    [Fact]
    public async Task StdioSpecIsEncodedWithCommandArgsAndEnvPairsAndNoType()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var stdio = new AcpStdioMcpServer(
            "gsd",
            "node",
            ["server.js", "--port", "7"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["ZETA"] = EnvSecretZ, ["ALPHA"] = EnvSecretA });
        var session = new AcpAgentSession(transport, Options([stdio]));
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);

        ParamsOf(transport, "session/new").ToJsonString().ShouldBe(
            $"{{\"cwd\":\"{Cwd}\",\"mcpServers\":[{{\"name\":\"gsd\",\"command\":\"node\",\"args\":[\"server.js\",\"--port\",\"7\"],\"env\":[{{\"name\":\"ALPHA\",\"value\":\"{EnvSecretA}\"}},{{\"name\":\"ZETA\",\"value\":\"{EnvSecretZ}\"}}]}}]}}");
    }

    /// <summary>A stdio spec with no arguments and no environment still carries empty <c>args</c> and <c>env</c> arrays.</summary>
    [Fact]
    public async Task StdioSpecWithNothingToPassKeepsEmptyArrays()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var stdio = new AcpStdioMcpServer("bare", "tool", [], new Dictionary<string, string>(StringComparer.Ordinal));
        var session = new AcpAgentSession(transport, Options([stdio]));
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);

        ParamsOf(transport, "session/new")["mcpServers"]!.ToJsonString().ShouldBe("[{\"name\":\"bare\",\"command\":\"tool\",\"args\":[],\"env\":[]}]");
    }

    /// <summary>An http spec has <c>type: "http"</c>, the URL and header pairs; an sse spec is identical with <c>type: "sse"</c>.</summary>
    [Fact]
    public async Task HttpAndSseSpecsCarryTypeUrlAndHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = HeaderSecret };
        var http = new AcpHttpMcpServer("substrate", new Uri("https://substrate.example/mcp"), headers);
        var sse = new AcpSseMcpServer("events", new Uri("https://events.example/sse"), new Dictionary<string, string>(StringComparer.Ordinal));
        var session = new AcpAgentSession(transport, Options([http, sse]));
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);

        ParamsOf(transport, "session/new")["mcpServers"]!.ToJsonString().ShouldBe(
            $"[{{\"type\":\"http\",\"name\":\"substrate\",\"url\":\"https://substrate.example/mcp\",\"headers\":[{{\"name\":\"Authorization\",\"value\":\"{HeaderSecret}\"}}]}},"
            + "{\"type\":\"sse\",\"name\":\"events\",\"url\":\"https://events.example/sse\",\"headers\":[]}]");
    }

    /// <summary><c>session/load</c> carries the same typed specs next to the session id and cwd.</summary>
    [Fact]
    public async Task SessionLoadCarriesTheSameSpecs()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var http = new AcpHttpMcpServer("substrate", new Uri("http://127.0.0.1:9/mcp"), new Dictionary<string, string>(StringComparer.Ordinal) { ["X-Key"] = "k" });
        var session = new AcpAgentSession(transport, Options([http]));
        await using var sessionScope = session.ConfigureAwait(true);

        await session.ResumeAsync(AcpFixtures.CapturedSessionId, ct).ConfigureAwait(true);

        ParamsOf(transport, "session/load").ToJsonString().ShouldBe(
            $"{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"cwd\":\"{Cwd}\",\"mcpServers\":[{{\"type\":\"http\",\"name\":\"substrate\",\"url\":\"http://127.0.0.1:9/mcp\",\"headers\":[{{\"name\":\"X-Key\",\"value\":\"k\"}}]}}]}}");
    }

    /// <summary>A duplicate name, a missing command or a non-http URL is rejected when the session is constructed, and the message never contains a URL or a secret.</summary>
    [Fact]
    public void InvalidSpecsAreRejectedAtConstructionWithoutLeakingValues()
    {
        var noHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        var secretHeaders = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = HeaderSecret };
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);

        var duplicate = Should.Throw<ArgumentException>(() => new AcpAgentSession(transport, Options(
            [new AcpHttpMcpServer("same", new Uri("https://a.example/mcp"), secretHeaders), new AcpHttpMcpServer("same", new Uri("https://b.example/mcp"), noHeaders)])));
        var noCommand = Should.Throw<ArgumentException>(() => new AcpAgentSession(transport, Options(
            [new AcpStdioMcpServer("tool", " ", [ArgSecret], noHeaders)])));
        var badUrl = Should.Throw<ArgumentException>(() => new AcpAgentSession(transport, Options(
            [new AcpHttpMcpServer("remote", new Uri("ftp://token-in-url.example/mcp?key=" + HeaderSecret), secretHeaders)])));

        duplicate.Message.ShouldContain("same");
        noCommand.Message.ShouldContain("tool");
        badUrl.Message.ShouldContain("remote");
        foreach (var failure in new[] { duplicate, noCommand, badUrl })
        {
            failure.ToString().ShouldNotContain("token-in-url", Case.Sensitive);
            foreach (var secret in AllSecrets)
            {
                failure.ToString().ShouldNotContain(secret, Case.Sensitive);
            }
        }
    }

    /// <summary>No spec prints an argument, a header value or an environment value in ToString, and neither does the options record that holds them.</summary>
    [Fact]
    public void ToStringNeverPrintsSecretValues()
    {
        var envs = new Dictionary<string, string>(StringComparer.Ordinal) { ["ALPHA"] = EnvSecretA, ["ZETA"] = EnvSecretZ };
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = HeaderSecret };
        AcpMcpServer[] servers =
        [
            new AcpStdioMcpServer("gsd", "node", [ArgSecret], envs),
            new AcpHttpMcpServer("substrate", new Uri("https://substrate.example/mcp?key=" + HeaderSecret), headers),
            new AcpSseMcpServer("events", new Uri("https://events.example/sse?key=" + HeaderSecret), headers),
        ];
        var options = Options(servers) with { EnvironmentVariables = envs };

        var texts = servers.Select(static server => $"{server}").Append($"{options}").ToArray();

        foreach (var text in texts)
        {
            foreach (var secret in AllSecrets)
            {
                text.ShouldNotContain(secret, Case.Sensitive);
            }
        }

        texts[0].ShouldContain("gsd");
        texts[0].ShouldContain("EnvironmentVariableCount = 2");
        texts[1].ShouldContain("HeaderCount = 1");
    }

    /// <summary>A rejected <c>session/new</c> surfaces AcpRequestFailedHarnessException; its message and every log line hold no secret value.</summary>
    [Fact]
    public async Task RejectedSessionNewSurfacesFailureWithoutSecretsInMessageOrLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new CapturingLogger<AcpAgentSession>();
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/new")
            ? [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"error\":{{\"code\":-32602,\"message\":\"MCP server substrate is unreachable\"}}}}")]
            : AcpFixtures.Reply(frame));
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = HeaderSecret };
        var envs = new Dictionary<string, string>(StringComparer.Ordinal) { ["ALPHA"] = EnvSecretA, ["ZETA"] = EnvSecretZ };
        AcpMcpServer[] servers =
        [
            new AcpStdioMcpServer("gsd", "node", [ArgSecret], envs),
            new AcpHttpMcpServer("substrate", new Uri("https://substrate.example/mcp"), headers),
        ];
        var session = new AcpAgentSession(transport, Options(servers), null, TimeSpan.FromSeconds(30), logger);
        await using var sessionScope = session.ConfigureAwait(true);

        var failure = await Should.ThrowAsync<AcpRequestFailedHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

        failure.Context.ShouldBe("session/new");
        failure.ErrorCode.ShouldBe(-32602);
        session.State.ShouldBe(HarnessState.Faulted);
        // The secrets did go out on the wire (that is the point of the spec) and nowhere else.
        AcpFixtures.Text(transport.Sent.Single(static frame => HasMethod(frame, "session/new"))).ShouldContain(HeaderSecret);
        foreach (var secret in AllSecrets)
        {
            failure.ToString().ShouldNotContain(secret, Case.Sensitive);
            foreach (var entry in logger.Entries)
            {
                entry.Message.ShouldNotContain(secret, Case.Sensitive);
                (entry.Exception?.ToString() ?? string.Empty).ShouldNotContain(secret, Case.Sensitive);
            }
        }
    }

    private static AcpClientOptions Options(IReadOnlyList<AcpMcpServer> servers) =>
        new() { WorkingDirectory = Cwd, AuthMethodId = "agent", McpServers = servers };

    private static JsonNode ParamsOf(ScriptedAcpTransport transport, string method) =>
        JsonNode.Parse(transport.Sent.Single(frame => HasMethod(frame, method)))!["params"]!;

    private static bool HasMethod(byte[] frame, string method) =>
        string.Equals(AcpFixtures.MethodOf(frame), method, StringComparison.Ordinal);
}
