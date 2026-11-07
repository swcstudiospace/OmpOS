namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// The Dispatch page: the devices, the command list, and this desktop's pairing and policy when the head has an <see cref="IThisDeviceHost"/>.
/// The web head passes none, so it has no pairing section; its pair text says to pair in the desktop app.
/// </summary>
/// <param name="Devices">The devices.</param>
/// <param name="Commands">The commands, newest first.</param>
/// <param name="PairThisDevice">This desktop's pairing and policy, or null when the head has no local device.</param>
/// <param name="Problem">The problem the page announces, or null: a list that could not be read, or an action that failed.</param>
public sealed record DispatchPageModel(
    DevicesViewModel Devices,
    IReadOnlyList<DispatchCommandRowModel> Commands,
    PairThisDeviceModel? PairThisDevice,
    string? Problem = null)
{
    /// <summary>
    /// Gets the failure lines the page announces, in order: an unreachable service, then the problem, then each failed command. A problem that
    /// repeats the unreachable line is shown once.
    /// </summary>
    public IReadOnlyList<string> Failures
    {
        get
        {
            var lines = new List<string>();
            if (Devices.Phase == DispatchPhase.Unreachable)
            {
                lines.Add(Devices.StatusText);
            }

            if (Problem is { } problem && !lines.Contains(problem, StringComparer.Ordinal))
            {
                lines.Add(problem);
            }

            lines.AddRange(Commands.Where(command => command.Phase == DispatchPhase.Failed).Select(command => command.StatusText));
            return lines;
        }
    }
}
