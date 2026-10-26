namespace OmpOS.Workspace;

/// <summary>
/// The result of <see cref="SetupRunGate.RunAsync"/>: either the run was started with the setup environment, or it was
/// not started and <see cref="Failure"/> says why.
/// </summary>
/// <param name="Environment">The variables the started run received. Empty when no run was started.</param>
/// <param name="Failure">Why no run was started, or null when the run was started.</param>
public sealed record SetupGateResult(IReadOnlyDictionary<string, string> Environment, SetupGateFailure? Failure)
{
    /// <summary>
    /// Gets a value indicating whether the setup passed and the run was started.
    /// </summary>
    public bool RunStarted => Failure is null;

    /// <summary>
    /// Creates the result of a run that was started.
    /// </summary>
    /// <param name="environment">The variables the run received.</param>
    public static SetupGateResult Started(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return new SetupGateResult(environment, null);
    }

    /// <summary>
    /// Creates the result of a run that was not started.
    /// </summary>
    /// <param name="failure">Why no run was started.</param>
    public static SetupGateResult Stopped(SetupGateFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new SetupGateResult(new Dictionary<string, string>(StringComparer.Ordinal), failure);
    }
}
