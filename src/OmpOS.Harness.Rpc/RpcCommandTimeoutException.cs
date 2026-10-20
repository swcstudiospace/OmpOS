using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>omp did not answer a correlated command in time. The session is faulted because the stream state is unknown.</summary>
public sealed class RpcCommandTimeoutException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="RpcCommandTimeoutException"/> class.</summary>
    public RpcCommandTimeoutException()
        : base("omp did not answer an RPC command in time.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RpcCommandTimeoutException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public RpcCommandTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RpcCommandTimeoutException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public RpcCommandTimeoutException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RpcCommandTimeoutException"/> class.</summary>
    /// <param name="command">The command type that went unanswered.</param>
    /// <param name="id">The correlation id of the unanswered command.</param>
    /// <param name="context">Optional context.</param>
    public RpcCommandTimeoutException(string command, string id, string? context)
        : base($"omp did not answer '{command}' (id {id}) in time.", context, null)
    {
        Command = command;
        CommandId = id;
    }

    /// <summary>Gets the command type that went unanswered.</summary>
    public string? Command { get; }

    /// <summary>Gets the correlation id of the unanswered command.</summary>
    public string? CommandId { get; }
}
