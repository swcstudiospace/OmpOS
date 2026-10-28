using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// The API's ACP head signs in the way omp 18.12.x requires. omp advertises <c>authMethods: [{id:"agent"}]</c> in every environment, and a
/// client that sends no <c>authenticate</c> fails its handshake before a session exists. The peer here is scripted to behave that way: it
/// answers <c>session/new</c> only after an <c>authenticate</c> with the <c>agent</c> method.
/// </summary>
public sealed class AcpAuthenticationHandshakeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A run opened with its session and run identity signs in with omp's method, waits for omp's slow first answer, and gets its session.</summary>
    [Fact]
    public async Task ARunSessionSignsInWithTheAgentMethodBeforeItOpens()
    {
        var peers = new ConcurrentQueue<AuthenticatingPeer>();
        var factory = CreateFactory(new OmpAcpClient(), options => NewTransport(peers, options));

        var session = factory.CreateSession("prompt", "session-auth", "run-auth");
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(Token).ConfigureAwait(true);

            var peer = peers.ShouldHaveSingleItem();
            peer.Methods.ShouldBe(["initialize", "authenticate", "session/new"]);
            peer.AuthenticatedWith.ShouldBe(AcpClientOptions.OmpAuthMethodId);
            peer.Options.HandshakeTimeout.ShouldBe(AcpClientOptions.OmpHandshakeTimeout);
        }
    }

    /// <summary>The prompt-only overload, which keeps the shared client, signs in the same way.</summary>
    [Fact]
    public async Task ThePromptOnlySessionSignsInWithTheAgentMethodBeforeItOpens()
    {
        var peers = new ConcurrentQueue<AuthenticatingPeer>();
        var factory = CreateFactory(new OmpAcpClient(options => NewTransport(peers, options)), static _ => throw new InvalidOperationException("The per-run transport must not be used."));

        var session = factory.CreateSession("prompt");
        await using (session.ConfigureAwait(true))
        {
            await session.CreateAsync(Token).ConfigureAwait(true);

            var peer = peers.ShouldHaveSingleItem();
            peer.Methods.ShouldBe(["initialize", "authenticate", "session/new"]);
            peer.AuthenticatedWith.ShouldBe("agent");
            peer.Options.HandshakeTimeout.ShouldBe(AcpClientOptions.OmpHandshakeTimeout);
        }
    }

    private static AuthenticatingTransport NewTransport(ConcurrentQueue<AuthenticatingPeer> peers, AcpClientOptions options)
    {
        var peer = new AuthenticatingPeer(options);
        peers.Enqueue(peer);
        return new AuthenticatingTransport(peer);
    }

    private static AcpHarnessSessionFactory CreateFactory(OmpAcpClient client, Func<AcpClientOptions, IAcpTransport> transportFactory)
    {
        var repository = new InMemorySessionRepository();
        return new AcpHarnessSessionFactory(
            client,
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions { Adapter = HarnessOptions.AcpAdapter }),
            repository,
            new SessionService(repository, TimeProvider.System),
            transportFactory,
            AcpApprovalPolicy.DenyAll,
            TimeSpan.FromSeconds(30));
    }

    /// <summary>An omp that advertises one authentication method and refuses <c>session/new</c> until the client has authenticated with it.</summary>
    private sealed class AuthenticatingPeer
    {
        private const string InitializeResult = "{\"protocolVersion\":1,\"agentInfo\":{\"name\":\"omp\",\"title\":\"omp\",\"version\":\"18.12.6\"},\"agentCapabilities\":{},\"authMethods\":[{\"id\":\"agent\",\"name\":\"Use existing local credentials\"}]}";

        private readonly List<string> _methods = [];

        public AuthenticatingPeer(AcpClientOptions options)
        {
            Options = options;
        }

        /// <summary>Gets the options the client was started with.</summary>
        public AcpClientOptions Options { get; }

        /// <summary>Gets the methods the client sent, in order.</summary>
        public IReadOnlyList<string> Methods
        {
            get
            {
                lock (_methods)
                {
                    return [.. _methods];
                }
            }
        }

        /// <summary>Gets the method id of the <c>authenticate</c> request, or null when the client sent none.</summary>
        public string? AuthenticatedWith { get; private set; }

        /// <summary>Answers one frame the client sent.</summary>
        /// <param name="frame">The frame the client sent.</param>
        /// <returns>The frames the peer sends back.</returns>
        public byte[][] Reply(byte[] frame)
        {
            var node = JsonNode.Parse(frame)!;
            var method = node["method"]?.GetValue<string>();
            var id = node["id"]?.GetValue<long>() ?? -1;
            if (method is null)
            {
                return [];
            }

            lock (_methods)
            {
                _methods.Add(method);
            }

            return method switch
            {
                "initialize" => [Utf8(Result(id, InitializeResult))],
                "authenticate" => Authenticate(id, node),
                "session/new" when AuthenticatedWith is null => [Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"error\":{{\"code\":-32000,\"message\":\"Authentication required\"}}}}")],
                "session/new" => [Utf8(Result(id, "{\"sessionId\":\"acp-session\"}"))],
                _ => [Utf8(Result(id, "{}"))],
            };
        }

        private static string Result(long id, string result) => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}";

        private static byte[] Utf8(string text) => System.Text.Encoding.UTF8.GetBytes(text);

        private byte[][] Authenticate(long id, JsonNode node)
        {
            AuthenticatedWith = node["params"]?["methodId"]?.GetValue<string>();
            return [Utf8(Result(id, "{}"))];
        }
    }

    /// <summary>An in-memory transport whose peer is an <see cref="AuthenticatingPeer"/>. Every sent frame is answered by the peer.</summary>
    private sealed class AuthenticatingTransport(AuthenticatingPeer peer) : IAcpTransport
    {
        private readonly Channel<byte[]> _inbound = Channel.CreateUnbounded<byte[]>();

        /// <inheritdoc />
        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var reply in peer.Reply(frame.ToArray()))
            {
                _ = _inbound.Writer.TryWrite(reply);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (true)
            {
                if (!await _inbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new DisconnectedHarnessException("The scripted peer closed.", "scripted");
                }

                while (_inbound.Reader.TryRead(out var frame))
                {
                    yield return frame;
                }
            }
        }

        /// <inheritdoc />
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            _ = _inbound.Writer.TryComplete();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _ = _inbound.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
