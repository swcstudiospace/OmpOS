using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>Behaviour of <see cref="AcpAgentSession"/> against a scripted agent.</summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default, and await using disposal is intentional.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class AcpAgentSessionTests
{
    private const long PermissionRequestId = 900;
    private const long UnsupportedRequestId = 901;
    private const long FsAnswerId = 910;

    /// <summary>Update notifications delivered with no consumer after the first, past the 4096-event turn buffer.</summary>
    private const int FloodUpdates = 5000;

    /// <summary>The turn buffer capacity: this many updates fill it exactly.</summary>
    private const int TurnBufferCapacity = 4096;

    private const string AllowAndRejectOptions =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"},{\"optionId\":\"reject-once\",\"name\":\"Reject\",\"kind\":\"reject_once\"}]";

    private const string AllowOnlyOption =
        "[{\"optionId\":\"allow-once\",\"name\":\"Allow\",\"kind\":\"allow_once\"}]";

    /// <summary>A prompt streams session/update notifications in wire order and ends with the stop reason.</summary>
    [Fact]
    public async Task PromptStreamMapsUpdatesInWireOrderAndEndsWithStopReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/prompt")
            ? PromptScript(AcpFixtures.IdOf(frame))
            : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct), ct).ConfigureAwait(true);

        events.Count.ShouldBe(7);
        events[0].ShouldBe(new TextDeltaEvent("Hel"));
        events[1].ShouldBe(new ThoughtEvent("hmm"));
        var call = events[2].ShouldBeOfType<ToolCallEvent>();
        call.CallId.ShouldBe("t1");
        call.Name.ShouldBe("Read file");
        var update = events[3].ShouldBeOfType<ToolCallUpdateEvent>();
        update.CallId.ShouldBe("t1");
        update.Status.ShouldBe("completed");
        events[4].ShouldBe(new PlanEvent("- [x] step one\n- [ ] step two"));
        events[5].ShouldBe(new TextDeltaEvent("lo"));
        events[6].ShouldBe(new CompletedEvent("end_turn"));
        session.State.ShouldBe(HarnessState.Ready);
    }

    /// <summary>A failed prompt ends with its ErrorEvent alone: no CompletedEvent follows it, and the session stays Ready.</summary>
    [Fact]
    public async Task PromptErrorEndsTurnWithErrorEventAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/prompt")
            ? [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"error\":{{\"code\":-32000,\"message\":\"model unavailable\"}}}}")]
            : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct), ct).ConfigureAwait(true);

        events.ShouldBe(new HarnessEvent[] { new ErrorEvent("model unavailable", Retryable: false) });
        session.State.ShouldBe(HarnessState.Ready);
    }

    /// <summary>Cancelling mid-stream sends session/cancel and $/cancel_request, then the prompt completes with the agent's stop reason.</summary>
    [Fact]
    public async Task CancelSendsCancelNotificationsAndEndsWithAgentStopReason()
    {
        var ct = TestContext.Current.CancellationToken;
        long promptId = 0;
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (HasMethod(frame, "session/prompt"))
            {
                promptId = AcpFixtures.IdOf(frame);
                return PartialChunk();
            }

            if (HasMethod(frame, "$/cancel_request"))
            {
                return [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{promptId},\"result\":{{\"stopReason\":\"cancelled\"}}}}")];
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await using var stream = session.SubmitPromptAsync("hi", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new TextDeltaEvent("Partial"));

        await session.CancelAsync(ct).ConfigureAwait(true);

        var rest = new List<HarnessEvent>();
        while (await stream.MoveNextAsync().ConfigureAwait(true))
        {
            rest.Add(stream.Current);
        }

        rest.ShouldBe(new HarnessEvent[] { new CompletedEvent("cancelled") });
        session.State.ShouldBe(HarnessState.Ready);

        var sentMethods = transport.Sent.Select(AcpFixtures.MethodOf).ToArray();
        sentMethods.Contains("session/cancel", StringComparer.Ordinal).ShouldBeTrue();
        var cancelRequest = transport.Sent.Single(frame => HasMethod(frame, "$/cancel_request"));
        JsonNode.Parse(cancelRequest)!["params"]!["requestId"]!.GetValue<long>().ShouldBe(promptId);
    }

    /// <summary>With the default policy a permission request is answered by selecting the reject option.</summary>
    [Fact]
    public async Task PermissionRequestIsDeniedBySelectingRejectOptionByDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await AnswerPermissionAsync(
            new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" },
            PermissionRequest("execute", AllowAndRejectOptions),
            ct).ConfigureAwait(true);

        var outcome = JsonNode.Parse(answer)!["result"]!["outcome"]!;
        outcome["outcome"]!.GetValue<string>().ShouldBe("selected");
        outcome["optionId"]!.GetValue<string>().ShouldBe("reject-once");
    }

    /// <summary>When the agent offered no reject option, a denial is answered as cancelled.</summary>
    [Fact]
    public async Task PermissionDenialWithoutRejectOptionIsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await AnswerPermissionAsync(
            new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" },
            PermissionRequest("execute", AllowOnlyOption),
            ct).ConfigureAwait(true);

        var outcome = JsonNode.Parse(answer)!["result"]!["outcome"]!;
        outcome["outcome"]!.GetValue<string>().ShouldBe("cancelled");
        outcome["optionId"].ShouldBeNull();
    }

    /// <summary>An allowlisted kind with no resolver is denied: the policy admits the request, but no decision can arrive to grant it.</summary>
    [Fact]
    public async Task AllowlistedPermissionWithoutResolverIsDeniedByRejectOption()
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await AnswerPermissionAsync(
            new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent", ApprovalPolicy = new AcpApprovalPolicy(["read"]) },
            PermissionRequest("read", AllowAndRejectOptions),
            ct).ConfigureAwait(true);

        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");
    }

    /// <summary>A permission request outside the allowlist is denied even when the narrowing hook accepts it.</summary>
    [Fact]
    public async Task NarrowingHookCannotGrantPermissionOutsideAllowlist()
    {
        var ct = TestContext.Current.CancellationToken;
        var answer = await AnswerPermissionAsync(
            new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent", ApprovalPolicy = new AcpApprovalPolicy(["read"], static _ => true) },
            PermissionRequest("execute", AllowAndRejectOptions),
            ct).ConfigureAwait(true);

        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");
    }

    /// <summary>Agent calls to fs methods are answered with method-not-found, because the client advertises no fs capability.</summary>
    [Fact]
    public async Task UnadvertisedFsMethodIsAnsweredWithMethodNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == UnsupportedRequestId)
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{UnsupportedRequestId},\"method\":\"fs/read_text_file\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"path\":\"/etc/hosts\"}}}}"));
        var answer = await answered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);

        JsonNode.Parse(answer)!["error"]!["code"]!.GetValue<long>().ShouldBe(-32601);
    }

    /// <summary>An initialize result without auth methods sends no authenticate request and the session becomes ready.</summary>
    [Fact]
    public async Task NoAuthMethodsMeansNoAuthenticateRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "initialize")
            ? [InitializeWithoutAuthMethods(AcpFixtures.IdOf(frame))]
            : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Ready);
        session.SessionId.ShouldBe(AcpFixtures.CapturedSessionId);
        transport.Sent.Any(static frame => HasMethod(frame, "authenticate")).ShouldBeFalse();
        transport.Sent.Any(static frame => HasMethod(frame, "session/new")).ShouldBeTrue();
    }

    /// <summary>Advertised auth methods plus a configured id send exactly one authenticate with that id before session/new.</summary>
    [Fact]
    public async Task AuthMethodsWithConfiguredIdSendsOneAuthenticateBeforeSessionNew()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var authenticates = transport.Sent.Where(static frame => HasMethod(frame, "authenticate")).ToArray();
        authenticates.Length.ShouldBe(1);
        JsonNode.Parse(authenticates[0])!["params"]!["methodId"]!.GetValue<string>().ShouldBe("agent");
        var methods = transport.Sent.Select(AcpFixtures.MethodOf).ToArray();
        Array.IndexOf(methods, "authenticate", 0).ShouldBeLessThan(Array.IndexOf(methods, "session/new", 0));
    }

    /// <summary>Advertised auth methods without a configured id fail the handshake instead of guessing a method.</summary>
    [Fact]
    public async Task AuthMethodsWithoutConfiguredIdFailsHandshake()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd });

        var failure = await Should.ThrowAsync<AcpRequestFailedHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);
        failure.Context.ShouldBe("authenticate");
        session.State.ShouldBe(HarnessState.Faulted);
        transport.Sent.Any(static frame => HasMethod(frame, "session/new")).ShouldBeFalse();
    }

    /// <summary>With FsRoot set, initialize advertises both fs capabilities as true.</summary>
    [Fact]
    public async Task FsRootAdvertisesReadAndWriteCapabilities()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
            await using var session = new AcpAgentSession(transport, FsOptions(root.FullName));
            await session.CreateAsync(ct).ConfigureAwait(true);

            var initialize = transport.Sent.Single(frame => HasMethod(frame, "initialize"));
            var fs = JsonNode.Parse(initialize)!["params"]!["clientCapabilities"]!["fs"]!;
            fs["readTextFile"]!.GetValue<bool>().ShouldBeTrue();
            fs["writeTextFile"]!.GetValue<bool>().ShouldBeTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>Without FsRoot, initialize advertises both fs capabilities as false.</summary>
    [Fact]
    public async Task NoFsRootAdvertisesFalseCapabilities()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        var initialize = transport.Sent.Single(frame => HasMethod(frame, "initialize"));
        var fs = JsonNode.Parse(initialize)!["params"]!["clientCapabilities"]!["fs"]!;
        fs["readTextFile"]!.GetValue<bool>().ShouldBeFalse();
        fs["writeTextFile"]!.GetValue<bool>().ShouldBeFalse();
    }

    /// <summary>Without FsRoot, both fs handlers answer with an error naming the capability as disabled.</summary>
    [Fact]
    public async Task NoFsRootRejectsFsCallsAsDisabled()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = FsOptions(null);

        var read = await AnswerFsAsync(options, FsReadRequest(FsAnswerId, "notes.txt"), FsAnswerId, ct).ConfigureAwait(true);
        read["error"]!["code"]!.GetValue<long>().ShouldBe(-32601);
        read["error"]!["message"]!.GetValue<string>().ShouldContain("disabled");

        var write = await AnswerFsAsync(options, FsWriteRequest(FsAnswerId, "notes.txt", "hi"), FsAnswerId, ct).ConfigureAwait(true);
        write["error"]!["code"]!.GetValue<long>().ShouldBe(-32601);
        write["error"]!["message"]!.GetValue<string>().ShouldContain("disabled");
    }

    /// <summary>With a root, a read inside returns the file content, honouring line and limit, for relative and absolute paths.</summary>
    [Fact]
    public async Task FsReadInsideRootReturnsContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            var dir = Path.Combine(root.FullName, "notes");
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "hello.txt"), "line1\nline2\nline3\n", ct).ConfigureAwait(true);
            var options = FsOptions(root.FullName);

            var full = await AnswerFsAsync(options, FsReadRequest(FsAnswerId, "notes/hello.txt"), FsAnswerId, ct).ConfigureAwait(true);
            full["result"]!["content"]!.GetValue<string>().ShouldBe("line1\nline2\nline3");

            var absolute = await AnswerFsAsync(options, FsReadRequest(FsAnswerId, Path.Combine(dir, "hello.txt")), FsAnswerId, ct).ConfigureAwait(true);
            absolute["result"]!["content"]!.GetValue<string>().ShouldBe("line1\nline2\nline3");

            var window = await AnswerFsAsync(options, FsReadRequest(FsAnswerId, "notes/hello.txt", line: 2, limit: 1), FsAnswerId, ct).ConfigureAwait(true);
            window["result"]!["content"]!.GetValue<string>().ShouldBe("line2");

            var pastEnd = await AnswerFsAsync(options, FsReadRequest(FsAnswerId, "notes/hello.txt", line: 9), FsAnswerId, ct).ConfigureAwait(true);
            pastEnd["result"]!["content"]!.GetValue<string>().ShouldBe(string.Empty);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>A root that is a filesystem root admits a file beneath it, read by absolute path.</summary>
    [Fact]
    public async Task FsRootAtFilesystemRootAdmitsFileBeneathIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            var file = Path.Combine(dir.FullName, "hello.txt");
            await File.WriteAllTextAsync(file, "hello", ct).ConfigureAwait(true);

            var answer = await AnswerFsAsync(FsOptions(Path.GetPathRoot(file)), FsReadRequest(FsAnswerId, file), FsAnswerId, ct).ConfigureAwait(true);

            answer["result"]!["content"]!.GetValue<string>().ShouldBe("hello");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>A root spelled with a trailing separator admits a relative file beneath it.</summary>
    [Fact]
    public async Task FsRootWithTrailingSeparatorAdmitsRelativeFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "hello.txt"), "hello", ct).ConfigureAwait(true);

            var answer = await AnswerFsAsync(FsOptions(root.FullName + Path.DirectorySeparatorChar), FsReadRequest(FsAnswerId, "hello.txt"), FsAnswerId, ct).ConfigureAwait(true);

            answer["result"]!["content"]!.GetValue<string>().ShouldBe("hello");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>Reads that leave the root by traversal or by absolute path are rejected.</summary>
    [Fact]
    public async Task FsReadOutsideRootIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var outer = Directory.CreateTempSubdirectory("acp-fs-outer-");
        var root = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(outer.FullName, "outside.txt"), "secret", ct).ConfigureAwait(true);
            var options = FsOptions(root.FullName);

            var traversal = await AnswerFsAsync(options, FsReadRequest(FsAnswerId, $"../{outer.Name}/outside.txt"), FsAnswerId, ct).ConfigureAwait(true);
            traversal["error"]!["code"]!.GetValue<long>().ShouldBe(-32602);

            var absolute = await AnswerFsAsync(options, FsReadRequest(FsAnswerId, Path.Combine(outer.FullName, "outside.txt")), FsAnswerId, ct).ConfigureAwait(true);
            absolute["error"]!["code"]!.GetValue<long>().ShouldBe(-32602);
        }
        finally
        {
            root.Delete(recursive: true);
            outer.Delete(recursive: true);
        }
    }

    /// <summary>A read through a symlink that escapes the root is rejected.</summary>
    [Fact]
    public async Task FsReadThroughSymlinkEscapeIsRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var outer = Directory.CreateTempSubdirectory("acp-fs-outer-");
        var root = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            var secret = Path.Combine(outer.FullName, "secret.txt");
            await File.WriteAllTextAsync(secret, "secret", ct).ConfigureAwait(true);
            _ = File.CreateSymbolicLink(Path.Combine(root.FullName, "link.txt"), secret);

            var answer = await AnswerFsAsync(FsOptions(root.FullName), FsReadRequest(FsAnswerId, "link.txt"), FsAnswerId, ct).ConfigureAwait(true);

            answer["error"]!["code"]!.GetValue<long>().ShouldBe(-32602);
        }
        finally
        {
            root.Delete(recursive: true);
            outer.Delete(recursive: true);
        }
    }

    /// <summary>With a root, a write inside creates the file with its parent directories.</summary>
    [Fact]
    public async Task FsWriteInsideRootCreatesFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            var answer = await AnswerFsAsync(
                FsOptions(root.FullName),
                FsWriteRequest(FsAnswerId, "sub/deep/new.txt", "hello"),
                FsAnswerId,
                ct).ConfigureAwait(true);

            answer["error"].ShouldBeNull();
            (await File.ReadAllTextAsync(Path.Combine(root.FullName, "sub", "deep", "new.txt"), ct).ConfigureAwait(true)).ShouldBe("hello");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    /// <summary>A write outside the root is rejected and creates nothing.</summary>
    [Fact]
    public async Task FsWriteOutsideRootIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var outer = Directory.CreateTempSubdirectory("acp-fs-outer-");
        var root = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            var evil = Path.Combine(outer.FullName, "evil.txt");
            var options = FsOptions(root.FullName);

            var traversal = await AnswerFsAsync(options, FsWriteRequest(FsAnswerId, $"../{outer.Name}/evil.txt", "hi"), FsAnswerId, ct).ConfigureAwait(true);
            traversal["error"]!["code"]!.GetValue<long>().ShouldBe(-32602);

            var absolute = await AnswerFsAsync(options, FsWriteRequest(FsAnswerId, evil, "hi"), FsAnswerId, ct).ConfigureAwait(true);
            absolute["error"]!["code"]!.GetValue<long>().ShouldBe(-32602);

            File.Exists(evil).ShouldBeFalse();
        }
        finally
        {
            root.Delete(recursive: true);
            outer.Delete(recursive: true);
        }
    }

    /// <summary>A write through a symlinked directory that escapes the root is rejected and creates nothing outside.</summary>
    [Fact]
    public async Task FsWriteThroughSymlinkEscapeIsRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var outer = Directory.CreateTempSubdirectory("acp-fs-outer-");
        var root = Directory.CreateTempSubdirectory("acp-fs-");
        try
        {
            _ = Directory.CreateSymbolicLink(Path.Combine(root.FullName, "evildir"), outer.FullName);
            var evil = Path.Combine(outer.FullName, "evil.txt");

            var answer = await AnswerFsAsync(
                FsOptions(root.FullName),
                FsWriteRequest(FsAnswerId, "evildir/evil.txt", "hi"),
                FsAnswerId,
                ct).ConfigureAwait(true);

            answer["error"]!["code"]!.GetValue<long>().ShouldBe(-32602);
            File.Exists(evil).ShouldBeFalse();
        }
        finally
        {
            root.Delete(recursive: true);
            outer.Delete(recursive: true);
        }
    }

    private static AcpClientOptions FsOptions(string? fsRoot) =>
        new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent", FsRoot = fsRoot };

    private static byte[] FsReadRequest(long id, string path, int? line = null, int? limit = null)
    {
        var paramsNode = new JsonObject { ["sessionId"] = AcpFixtures.CapturedSessionId, ["path"] = path };
        if (line.HasValue)
        {
            paramsNode["line"] = line.Value;
        }

        if (limit.HasValue)
        {
            paramsNode["limit"] = limit.Value;
        }

        return AcpFixtures.Utf8(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "fs/read_text_file",
            ["params"] = paramsNode,
        }.ToJsonString());
    }

    private static byte[] FsWriteRequest(long id, string path, string content) =>
        AcpFixtures.Utf8(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "fs/write_text_file",
            ["params"] = new JsonObject
            {
                ["sessionId"] = AcpFixtures.CapturedSessionId,
                ["path"] = path,
                ["content"] = content,
            },
        }.ToJsonString());

    private static async Task<JsonNode> AnswerFsAsync(AcpClientOptions options, byte[] request, long answerId, CancellationToken ct)
    {
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == answerId)
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = new AcpAgentSession(transport, options);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(request);
        var answer = await answered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);
        return JsonNode.Parse(answer)!;
    }

    private static byte[] InitializeWithoutAuthMethods(long id)
    {
        var node = JsonNode.Parse(AcpFixtures.Lines("acp-out.ndjson")[0])!;
        node["result"]!.AsObject().Remove("authMethods");
        node["id"] = id;
        return AcpFixtures.Utf8(node.ToJsonString());
    }

    /// <summary>A silent agent fails the handshake with HandshakeTimeoutHarnessException and faults the session.</summary>
    [Fact]
    public async Task SilentAgentFailsHandshakeAndFaultsSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "initialize") ? [] : AcpFixtures.Reply(frame));
        await using var session = new AcpAgentSession(transport, new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            HandshakeTimeout = TimeSpan.FromMilliseconds(100),
        });

        await Should.ThrowAsync<HandshakeTimeoutHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>An agent that selects a protocol version other than 1 fails the handshake.</summary>
    [Fact]
    public async Task ProtocolVersionMismatchFailsHandshake()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "initialize")
            ? [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":{{\"protocolVersion\":2,\"agentInfo\":{{\"name\":\"omp\"}}}}}}")]
            : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);

        await Should.ThrowAsync<AcpRequestFailedHarnessException>(() => session.CreateAsync(ct)).ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>The agent dropping the connection mid-prompt ends the stream with DisconnectedHarnessException and faults the session.</summary>
    [Fact]
    public async Task TransportDropDuringPromptFaultsSession()
    {
        var ct = TestContext.Current.CancellationToken;
        ScriptedAcpTransport? transport = null;
        transport = new ScriptedAcpTransport(frame =>
        {
            if (HasMethod(frame, "session/prompt"))
            {
                transport!.Disconnect();
                return [];
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<DisconnectedHarnessException>(() => CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct), ct))
            .ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Faulted);
    }

    /// <summary>Steer has no ACP v1 equivalent and is rejected rather than silently queued.</summary>
    [Fact]
    public async Task SteerIsRejectedBecauseAcpHasNoSteer()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = NewSession(new ScriptedAcpTransport(AcpFixtures.Reply));
        await session.CreateAsync(ct).ConfigureAwait(true);

        await Should.ThrowAsync<NotSupportedException>(() => CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions { Steer = true }, ct), ct))
            .ConfigureAwait(true);
    }

    /// <summary>ResumeAsync loads the given session with session/load and reports it as the session id.</summary>
    [Fact]
    public async Task ResumeLoadsTheRequestedSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var session = NewSession(transport);

        await session.ResumeAsync(AcpFixtures.CapturedSessionId, ct).ConfigureAwait(true);

        session.SessionId.ShouldBe(AcpFixtures.CapturedSessionId);
        session.State.ShouldBe(HarnessState.Ready);
        var load = transport.Sent.Single(frame => HasMethod(frame, "session/load"));
        JsonNode.Parse(load)!["params"]!["sessionId"]!.GetValue<string>().ShouldBe(AcpFixtures.CapturedSessionId);
    }

    /// <summary>Disposing a ready session ends it on the agent with session/close for its id.</summary>
    [Fact]
    public async Task DisposeClosesTheSessionOnTheWire()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await session.DisposeAsync().ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Disposed);
        var close = transport.Sent.Single(frame => HasMethod(frame, "session/close"));
        JsonNode.Parse(close)!["params"]!["sessionId"]!.GetValue<string>().ShouldBe(AcpFixtures.CapturedSessionId);
    }

    /// <summary>A completion that finds the turn buffer full fails the turn with BackpressureHarnessException and faults the session instead of ending cleanly.</summary>
    [Fact]
    public async Task CompletionWithFullTurnBufferFailsTurnWithBackpressure()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/prompt") ? [] : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await using var stream = session.SubmitPromptAsync("hi", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        var firstMove = stream.MoveNextAsync();
        var prompt = await WaitForFrameAsync(transport, frame => HasMethod(frame, "session/prompt"), ct).ConfigureAwait(true);
        transport.Deliver(Update(AcpFixtures.CapturedSessionId, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"first\"}}"));
        (await firstMove.ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new TextDeltaEvent("first"));

        for (var i = 0; i < TurnBufferCapacity; i++)
        {
            transport.Deliver(Update(AcpFixtures.CapturedSessionId, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"flood\"}}"));
        }

        transport.Deliver(AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(prompt)},\"result\":{{\"stopReason\":\"end_turn\"}}}}"));

        await WaitForStateAsync(session, HarnessState.Faulted, ct).ConfigureAwait(true);
        await Should.ThrowAsync<BackpressureHarnessException>(DrainAsync).ConfigureAwait(true);

        async Task DrainAsync()
        {
            while (await stream.MoveNextAsync().ConfigureAwait(true))
            {
            }
        }
    }

    /// <summary>A session/update flood past the turn buffer fails the turn with BackpressureHarnessException and faults the session.</summary>
    [Fact]
    public async Task UpdateFloodBeyondCapacityFailsTurnWithBackpressureAndFaultsSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/prompt") ? [] : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await using var stream = session.SubmitPromptAsync("hi", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        var firstMove = stream.MoveNextAsync();
        transport.Deliver(Update(AcpFixtures.CapturedSessionId, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"first\"}}"));
        (await firstMove.ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new TextDeltaEvent("first"));

        for (var i = 0; i < FloodUpdates; i++)
        {
            transport.Deliver(Update(AcpFixtures.CapturedSessionId, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"flood\"}}"));
        }

        await WaitForStateAsync(session, HarnessState.Faulted, ct).ConfigureAwait(true);

        await Should.ThrowAsync<BackpressureHarnessException>(DrainAsync).ConfigureAwait(true);

        async Task DrainAsync()
        {
            while (await stream.MoveNextAsync().ConfigureAwait(true))
            {
            }
        }
    }

    /// <summary>A throwing narrowing hook fails closed: the request is denied and the failure is recorded as a diagnostic event.</summary>
    [Fact]
    public async Task ThrowingApprovalHookDeniesAndRecordsDiagnostic()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/prompt") ? [] : AcpFixtures.Reply(frame));
        var options = new AcpClientOptions
        {
            WorkingDirectory = AcpFixtures.CapturedCwd,
            AuthMethodId = "agent",
            ApprovalPolicy = new AcpApprovalPolicy(["execute"], static _ => throw new InvalidOperationException("hook blew up")),
        };
        await using var session = new AcpAgentSession(transport, options);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await using var stream = session.SubmitPromptAsync("hi", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        var firstMove = stream.MoveNextAsync();
        transport.Deliver(Update(AcpFixtures.CapturedSessionId, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"first\"}}"));
        (await firstMove.ConfigureAwait(true)).ShouldBeTrue();
        var prompt = await WaitForFrameAsync(transport, frame => HasMethod(frame, "session/prompt"), ct).ConfigureAwait(true);

        transport.Deliver(PermissionRequest("execute", AllowAndRejectOptions));

        var answer = await WaitForFrameAsync(transport, static frame => AcpFixtures.MethodOf(frame) is null, ct).ConfigureAwait(true);
        JsonNode.Parse(answer)!["result"]!["outcome"]!["optionId"]!.GetValue<string>().ShouldBe("reject-once");

        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        var diagnostic = stream.Current.ShouldBeOfType<DiagnosticEvent>();
        diagnostic.Message.ShouldContain("InvalidOperationException");

        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBeOfType<DiagnosticEvent>().Message.ShouldContain("denied");

        transport.Deliver(AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(prompt)},\"result\":{{\"stopReason\":\"end_turn\"}}}}"));
        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new CompletedEvent("end_turn"));
        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>A diagnostic is non-terminal: it leaves the turn running, and the turn still ends with its CompletedEvent.</summary>
    [Fact]
    public async Task DiagnosticDoesNotEndTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame => HasMethod(frame, "session/prompt") ? [] : AcpFixtures.Reply(frame));
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        await using var stream = session.SubmitPromptAsync("hi", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        var firstMove = stream.MoveNextAsync();
        var prompt = await WaitForFrameAsync(transport, frame => HasMethod(frame, "session/prompt"), ct).ConfigureAwait(true);
        transport.Deliver(Update(AcpFixtures.CapturedSessionId, "\"not-an-object\""));
        (await firstMove.ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBeOfType<DiagnosticEvent>();

        transport.Deliver(Update(AcpFixtures.CapturedSessionId, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"after\"}}"));
        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new TextDeltaEvent("after"));

        transport.Deliver(AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(prompt)},\"result\":{{\"stopReason\":\"end_turn\"}}}}"));
        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBe(new CompletedEvent("end_turn"));
        (await stream.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();
        session.State.ShouldBe(HarnessState.Ready);
    }

    /// <summary>A non-harness pump failure ends the stream with the real exception as the cause, faults the session, and never escapes disposal.</summary>
    [Fact]
    public async Task PumpFailurePreservesCauseAndDisposeDoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null)
            {
                throw new InvalidOperationException("answer send blew up");
            }

            return HasMethod(frame, "session/prompt") ? [] : AcpFixtures.Reply(frame);
        });
        await using var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent", ApprovalPolicy = new AcpApprovalPolicy(["execute"]) });
        await session.CreateAsync(ct).ConfigureAwait(true);

        await using var stream = session.SubmitPromptAsync("hi", new PromptOptions(), ct).GetAsyncEnumerator(ct);
        var firstMove = stream.MoveNextAsync();
        transport.Deliver(PermissionRequest("execute", AllowAndRejectOptions));

        // The permission request is announced before its answer send fails, so the first move still yields it.
        (await firstMove.ConfigureAwait(true)).ShouldBeTrue();
        stream.Current.ShouldBeOfType<PermissionRequestEvent>();

        var failure = await Should.ThrowAsync<DisconnectedHarnessException>(DrainAsync).ConfigureAwait(true);
        failure.InnerException.ShouldBeOfType<InvalidOperationException>();
        session.State.ShouldBe(HarnessState.Faulted);

        // Disposal must not throw the pump's recorded failure back out.
        await session.DisposeAsync().ConfigureAwait(true);

        async Task DrainAsync()
        {
            while (await stream.MoveNextAsync().ConfigureAwait(true))
            {
            }
        }
    }

    /// <summary>An agent request with a string id is answered with an error carrying that id, and the session stays healthy.</summary>
    [Fact]
    public async Task StringIdPermissionRequestIsAnsweredWithoutFaultingSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && HasRawId(frame, "perm-1"))
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = NewSession(transport);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":\"perm-1\",\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"toolCall\":{{\"toolCallId\":\"c1\",\"title\":\"Run it\",\"kind\":\"execute\"}},\"options\":{AllowAndRejectOptions}}}}}"));

        var answer = await answered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);

        var node = JsonNode.Parse(answer)!;
        node["id"]!.GetValue<string>().ShouldBe("perm-1");
        node["error"]!["code"]!.GetValue<long>().ShouldBe(-32602);
        session.State.ShouldBe(HarnessState.Ready);
    }

    private static AcpAgentSession NewSession(IAcpTransport transport) =>
        new(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });

    private static bool HasMethod(byte[] frame, string method) =>
        string.Equals(AcpFixtures.MethodOf(frame), method, StringComparison.Ordinal);

    private static bool HasRawId(byte[] frame, string id)
    {
        var nodeId = JsonNode.Parse(frame)?["id"];
        return nodeId is not null && nodeId.GetValueKind() is JsonValueKind.String
            && string.Equals(nodeId.GetValue<string>(), id, StringComparison.Ordinal);
    }

    private static async Task<byte[]> WaitForFrameAsync(ScriptedAcpTransport transport, Func<byte[], bool> match, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var hit = transport.Sent.FirstOrDefault(match);
            if (hit is not null)
            {
                return hit;
            }

            await Task.Delay(10, ct).ConfigureAwait(true);
        }

        throw new TimeoutException("The expected frame was not sent within 10 seconds.");
    }

    private static async Task WaitForStateAsync(AcpAgentSession session, HarnessState state, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (session.State != state && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10, ct).ConfigureAwait(true);
        }

        session.State.ShouldBe(state);
    }

    private static byte[] PermissionRequest(string kind, string options) =>
        AcpFixtures.Utf8(
            $"{{\"jsonrpc\":\"2.0\",\"id\":{PermissionRequestId},\"method\":\"session/request_permission\",\"params\":{{\"sessionId\":\"{AcpFixtures.CapturedSessionId}\",\"toolCall\":{{\"toolCallId\":\"c1\",\"title\":\"Run it\",\"kind\":\"{kind}\"}},\"options\":{options}}}}}");

    private static async Task<byte[]> AnswerPermissionAsync(AcpClientOptions options, byte[] request, CancellationToken ct)
    {
        var answered = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ScriptedAcpTransport(frame =>
        {
            if (AcpFixtures.MethodOf(frame) is null && AcpFixtures.IdOf(frame) == PermissionRequestId)
            {
                answered.TrySetResult(frame);
            }

            return AcpFixtures.Reply(frame);
        });
        await using var session = new AcpAgentSession(transport, options);
        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Deliver(request);
        return await answered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);
    }

    private static IEnumerable<byte[]> PartialChunk() =>
        [Update(AcpFixtures.CapturedSessionId, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"Partial\"}}")];

    private static IEnumerable<byte[]> PromptScript(long id)
    {
        var sid = AcpFixtures.CapturedSessionId;
        return
        [
            Update(sid, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"Hel\"}}"),
            Update(sid, "{\"sessionUpdate\":\"agent_thought_chunk\",\"content\":{\"type\":\"text\",\"text\":\"hmm\"}}"),
            Update(sid, "{\"sessionUpdate\":\"tool_call\",\"toolCallId\":\"t1\",\"title\":\"Read file\",\"kind\":\"read\",\"status\":\"pending\"}"),
            Update(sid, "{\"sessionUpdate\":\"tool_call_update\",\"toolCallId\":\"t1\",\"status\":\"completed\"}"),
            Update(sid, "{\"sessionUpdate\":\"plan\",\"entries\":[{\"content\":\"step one\",\"priority\":\"high\",\"status\":\"completed\"},{\"content\":\"step two\",\"priority\":\"medium\",\"status\":\"pending\"}]}"),
            Update(sid, "{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"lo\"}}"),
            AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"stopReason\":\"end_turn\"}}}}"),
        ];
    }

    private static byte[] Update(string sessionId, string update) =>
        AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{{\"sessionId\":\"{sessionId}\",\"update\":{update}}}}}");

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> stream, CancellationToken ct)
    {
        var events = new List<HarnessEvent>();
        await foreach (var harnessEvent in stream.WithCancellation(ct).ConfigureAwait(true))
        {
            events.Add(harnessEvent);
        }

        return events;
    }
}
