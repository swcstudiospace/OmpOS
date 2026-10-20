using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>omp answered a command with <c>success: false</c>. The session stays usable.</summary>
public sealed class RpcCommandException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="RpcCommandException"/> class.</summary>
    public RpcCommandException()
        : base("An omp RPC command failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RpcCommandException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public RpcCommandException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RpcCommandException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public RpcCommandException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RpcCommandException"/> class.</summary>
    /// <param name="command">The command type omp rejected.</param>
    /// <param name="code">The machine-readable failure code, when omp provided one.</param>
    /// <param name="message">The failure text omp returned.</param>
    /// <param name="context">Optional context.</param>
    public RpcCommandException(string command, string? code, string message, string? context)
        : base(message, context, null)
    {
        Command = command;
        Code = code;
    }

    /// <summary>Gets the command type omp rejected.</summary>
    public string? Command { get; }

    /// <summary>Gets the machine-readable failure code, when omp provided one.</summary>
    public string? Code { get; }
}
