using Autonogrammer.Presentation.Safety;

namespace Autonogrammer.Native.Tests.Safety;

/// <summary>A scripted source of the Run mode page: one snapshot, every save recorded.</summary>
internal sealed class FakeRunModeSource : IRunModeSource
{
    /// <summary>Gets or sets the snapshot a load returns.</summary>
    public RunModeSnapshot Snapshot { get; set; } = new()
    {
        Values = new RunModeValues(RunModeChoice.AutoReview, ReadBoundaryChoice.System, ["git status"], [], []),
        EditPermissionsPath = "/w/.autonogrammer/permissions.json",
        EditSandboxPath = "/w/.autonogrammer/sandbox.json",
        OtherTerminalAllowlist = [new RunModeEntry("npm", "/home/u/.autonogrammer/permissions.json", IsEditable: false)],
        DenyRules = [new RunModeEntry("Shell(rm)", "/home/u/.autonogrammer/permissions.json", IsEditable: false)],
        Files = [new RunModeFileRow("/w/.autonogrammer/permissions.json", "Project", "permissions.json", "Read.", IsWinning: false)],
        Diagnostics = [new RunModeDiagnostic("/home/u/.cursor/permissions.json", "It is not valid JSON. It is ignored.", IsError: true)],
    };

    /// <summary>Gets every set of values saved.</summary>
    public List<RunModeValues> Saved { get; } = [];

    /// <inheritdoc/>
    public Task<RunModeSnapshot> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Snapshot);

    /// <inheritdoc/>
    public Task SaveAsync(RunModeValues values, CancellationToken cancellationToken)
    {
        Saved.Add(values);
        Snapshot = Snapshot with { Values = values };
        return Task.CompletedTask;
    }
}
