namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// What a check or a provision of one remote environment reported. Every outcome is one of the sealed cases: a checked state with its drift,
/// a request for confirmation, the outcome of the writes, an unreachable machine with the last snapshot, a probe answer that did not parse,
/// a platform that is not supported, or a desired state that could not be read. A failing machine is a case, never an exception.
/// </summary>
public abstract record RemoteProvisionResult;
