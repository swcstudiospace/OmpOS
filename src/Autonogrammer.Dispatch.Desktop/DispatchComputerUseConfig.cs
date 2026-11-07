namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The two configuration keys a dispatched Computer Use task also needs, read by the composition root from phase 73 options.</summary>
/// <param name="ComputerUseEnabled">Key <c>Cowork:ComputerUse:Enabled</c>.</param>
/// <param name="AllowDispatchSessions">Key <c>Cowork:ComputerUse:AllowDispatchSessions</c>.</param>
public readonly record struct DispatchComputerUseConfig(bool ComputerUseEnabled, bool AllowDispatchSessions);
