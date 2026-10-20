namespace OmpOS.Harness.Rpc;

/// <summary>
/// A command with a type omp does not implement. Used to verify that an unknown command surfaces as a typed
/// <c>success: false</c> response rather than a silent drop.
/// </summary>
public sealed record UnknownCommandProbe : RpcCommand
{
    private readonly string _commandType;

    /// <summary>Creates a probe that sends <paramref name="commandType"/> verbatim.</summary>
    /// <param name="commandType">The command type to send.</param>
    public UnknownCommandProbe(string commandType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);
        _commandType = commandType;
    }

    /// <inheritdoc />
    public override string Type => _commandType;
}
