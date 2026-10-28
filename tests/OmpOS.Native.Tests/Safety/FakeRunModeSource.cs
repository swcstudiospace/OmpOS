using OmpOS.Presentation.Safety;

namespace OmpOS.Native.Tests.Safety;

/// <summary>A scripted source of the Run mode page: one snapshot, every save recorded.</summary>
internal sealed class FakeRunModeSource : IRunModeSource
{
    /// <summary>Gets or sets the snapshot a load returns.</summary>
    public RunModeSnapshot Snapshot { get; set; } = new()
    {
        Values = new RunModeValues(RunModeChoice.AutoReview, ReadBoundaryChoice.System, ["git status"], [], []),
        EditPermissionsPath = "/w/.ompos/permissions.json",
        EditSandboxPath = "/w/.ompos/sandbox.json",
        OtherTerminalAllowlist = [new RunModeEntry("npm", "/home/u/.ompos/permissions.json", IsEditable: false)],
        DenyRules = [new RunModeEntry("Shell(rm)", "/home/u/.ompos/permissions.json", IsEditable: false)],
        Files = [new RunModeFileRow("/w/.ompos/permissions.json", "Project", "permissions.json", "Read.", IsWinning: false)],
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
