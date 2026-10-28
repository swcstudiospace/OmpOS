using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Harness.AgentLoop.Tests.Live;

/// <summary>
/// Drives a real <c>omp acp</c> child over <see cref="AcpStdioTransport"/> with raw JSON-RPC frames, so the test sees every
/// notification, in particular <c>available_commands_update</c>, which the production session drops. It refuses to send
/// <c>session/prompt</c>, so no model turn can happen through it.
/// </summary>
internal sealed class LiveAcpFrameClient : IAsyncDisposable
{
    private const string PromptMethod = "session/prompt";
    private const int StderrTailLines = 20;

    private readonly AcpStdioTransport _transport;
    private readonly CancellationTokenSource _pumpCancellation = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<long, TaskCompletionSource<LiveAcpResponse>> _pending = [];
    private readonly SortedSet<string> _commands = new(StringComparer.Ordinal);
    private readonly List<string> _sentMethods = [];
    private Task? _pump;
    private Exception? _pumpFault;
    private bool _pumpEnded;
    private long _nextId;
    private int _commandUpdates;

    /// <summary>Initializes a new instance of the <see cref="LiveAcpFrameClient"/> class.</summary>
    /// <param name="ompPath">The omp executable.</param>
    /// <param name="workingDirectory">The child's working directory.</param>
    /// <param name="arguments">The arguments after <c>acp</c>.</param>
    /// <param name="environment">The environment set on the child on top of the inherited one.</param>
    public LiveAcpFrameClient(
        string ompPath,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment)
    {
        _transport = new AcpStdioTransport(ompPath, workingDirectory, arguments, environment);
    }

    /// <summary>Gets the methods sent so far, in order.</summary>
    public IReadOnlyList<string> SentMethods
    {
        get
        {
            lock (_gate)
            {
                return _sentMethods.ToArray();
            }
        }
    }

    /// <summary>Gets the number of <c>available_commands_update</c> notifications received so far.</summary>
    public int CommandUpdateCount
    {
        get
        {
            lock (_gate)
            {
                return _commandUpdates;
            }
        }
    }

    /// <summary>Gets the distinct command names announced so far, ordinal-sorted, without a leading slash.</summary>
    public IReadOnlyList<string> CommandNames
    {
        get
        {
            lock (_gate)
            {
                return _commands.ToArray();
            }
        }
    }

    /// <summary>Starts the child and the frame pump.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _transport.ConnectAsync(cancellationToken).ConfigureAwait(true);
        _pump = Task.Run(() => PumpAsync(_pumpCancellation.Token), CancellationToken.None);
    }

    /// <summary>Sends one request and waits for its response.</summary>
    /// <param name="method">The ACP method. <c>session/prompt</c> is refused.</param>
    /// <param name="parameters">The request parameters.</param>
    /// <param name="timeout">How long to wait for the response.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The response, which may carry an error.</returns>
    /// <exception cref="InvalidOperationException">The method is <c>session/prompt</c>, or the child's output has ended.</exception>
    /// <exception cref="TimeoutException">No response arrived in time. The message ends with the child's last stderr lines.</exception>
    public async Task<LiveAcpResponse> RequestAsync(string method, JsonObject parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(parameters);
        if (string.Equals(method, PromptMethod, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The live handshake test never sends session/prompt.");
        }

        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<LiveAcpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_pumpEnded)
            {
                throw new InvalidOperationException("The omp acp output has ended: " + DescribeFault() + StderrTail());
            }

            _pending[id] = completion;
            _sentMethods.Add(method);
        }

        var frame = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        };
        await _transport.SendAsync(Encoding.UTF8.GetBytes(frame.ToJsonString()), cancellationToken).ConfigureAwait(true);
        try
        {
            return await completion.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(true);
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"No response to '{method}' within {timeout.TotalSeconds:F0} s." + StderrTail(), ex);
        }
    }

    /// <summary>Waits for a fixed time while the pump collects notifications.</summary>
    /// <param name="duration">How long to collect.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public static async Task CollectAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        await Task.Delay(duration, cancellationToken).ConfigureAwait(true);

    /// <summary>Returns the last stderr lines of the child, for failure messages.</summary>
    public string StderrTail()
    {
        var lines = new Queue<string>();
        while (_transport.StandardError.TryRead(out var line))
        {
            lines.Enqueue(line);
            if (lines.Count > StderrTailLines)
            {
                lines.Dequeue();
            }
        }

        return lines.Count == 0 ? string.Empty : Environment.NewLine + "omp stderr tail:" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    /// <summary>Closes stdin, gives the child a grace period, then kills its whole process tree.</summary>
    /// <param name="cancellationToken">Cancels the close.</param>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await _pumpCancellation.CancelAsync().ConfigureAwait(true);
        await _transport.CloseAsync(cancellationToken).ConfigureAwait(true);
        if (_pump is not null)
        {
            await _pump.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None).ConfigureAwait(true);
        await _transport.DisposeAsync().ConfigureAwait(true);
        _pumpCancellation.Dispose();
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        Exception? fault = null;
        try
        {
            await foreach (var frame in _transport.ReceiveAsync(cancellationToken).ConfigureAwait(true))
            {
                await HandleFrameAsync(frame, cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // The test closed the client.
        }
        catch (HarnessException ex)
        {
            fault = ex;
        }
        catch (JsonException ex)
        {
            fault = ex;
        }
        finally
        {
            EndPump(fault);
        }
    }

    private void EndPump(Exception? fault)
    {
        List<TaskCompletionSource<LiveAcpResponse>> orphans;
        lock (_gate)
        {
            _pumpEnded = true;
            _pumpFault = fault;
            orphans = [.. _pending.Values];
            _pending.Clear();
        }

        foreach (var orphan in orphans)
        {
            orphan.TrySetException(new InvalidOperationException("The omp acp output ended before the response: " + DescribeFault()));
        }
    }

    private string DescribeFault() => _pumpFault?.Message ?? "closed without a fault.";

    private async Task HandleFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(frame);
        var root = document.RootElement;
        var hasMethod = root.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String;
        var hasId = root.TryGetProperty("id", out var id);
        if (hasMethod && hasId)
        {
            await RefuseAgentRequestAsync(id, cancellationToken).ConfigureAwait(true);
        }
        else if (hasMethod)
        {
            RecordNotification(method.GetString()!, root);
        }
        else if (hasId && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var responseId))
        {
            Complete(responseId, root);
        }
    }

    private async Task RefuseAgentRequestAsync(JsonElement id, CancellationToken cancellationToken)
    {
        // Nothing the handshake needs is agent-initiated; refuse so the agent never waits on this client.
        var reply = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = JsonNode.Parse(id.GetRawText()),
            ["error"] = new JsonObject { ["code"] = -32601, ["message"] = "The live handshake test implements no client methods." },
        };
        await _transport.SendAsync(Encoding.UTF8.GetBytes(reply.ToJsonString()), cancellationToken).ConfigureAwait(true);
    }

    private void Complete(long id, JsonElement root)
    {
        TaskCompletionSource<LiveAcpResponse>? completion;
        lock (_gate)
        {
            _pending.Remove(id, out completion);
        }

        if (completion is null)
        {
            return;
        }

        if (root.TryGetProperty("error", out var error))
        {
            var code = error.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt64(out var value) ? value : 0;
            var message = error.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : null;
            completion.TrySetResult(new LiveAcpResponse(null, code, message ?? "(no message)"));
            return;
        }

        var result = root.TryGetProperty("result", out var resultElement) ? resultElement.Clone() : default(JsonElement?);
        completion.TrySetResult(new LiveAcpResponse(result, null, null));
    }

    private void RecordNotification(string method, JsonElement root)
    {
        if (!string.Equals(method, "session/update", StringComparison.Ordinal)
            || !root.TryGetProperty("params", out var parameters)
            || !parameters.TryGetProperty("update", out var update)
            || !update.TryGetProperty("sessionUpdate", out var kind)
            || !string.Equals(kind.GetString(), "available_commands_update", StringComparison.Ordinal)
            || !update.TryGetProperty("availableCommands", out var commands)
            || commands.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        lock (_gate)
        {
            _commandUpdates++;
            foreach (var command in commands.EnumerateArray())
            {
                if (command.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() is { Length: > 0 } text)
                {
                    _commands.Add(text.TrimStart('/'));
                }
            }
        }
    }
}
