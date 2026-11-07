using Autonogrammer.Presentation.Settings.Permissions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The Permissions models expose Manual, Auto (rules) and Skip, and round-trip folder grants.</summary>
public sealed class PermissionModesModelTests
{
    [Fact]
    public void ModesDefaultToManualAndNameAutoAndSkip()
    {
        var modes = new PermissionModesModel();
        modes.Selected.ShouldBe(PermissionModeChoice.Manual);
        modes.SelectedLabel.ShouldBe(PermissionModesModel.ManualLabel);
        modes.ModeLabels.ShouldBe([PermissionModesModel.ManualLabel, PermissionModesModel.AutoLabel, PermissionModesModel.SkipLabel]);
        PermissionModesModel.AutoLabel.ShouldBe("Auto (rules)");
        PermissionModesModel.SkipWarning.ShouldContain("acknowledgement");
        var statements = new PermissionStatementModel();
        statements.Statements.ShouldContain(PermissionStatementModel.DeleteProtectionAlwaysAsks, StringComparer.Ordinal);
        statements.Statements.ShouldContain(PermissionStatementModel.RevokedGrantsApplyNextSession, StringComparer.Ordinal);
    }

    [Fact]
    public void FolderGrantModelRoundTripsAccessAndScope()
    {
        var source = new FakeFolderGrantSource();
        var model = new FolderGrantModel(source);
        var saved = model.Save("/work", FolderAccessMode.ReadOnly, FolderGrantScope.Project);
        saved.Access.ShouldBe(FolderAccessMode.ReadOnly);
        saved.Scope.ShouldBe(FolderGrantScope.Project);
        model.Save("/work", FolderAccessMode.ReadWrite, FolderGrantScope.Global);
        model.Grants.Single().Access.ShouldBe(FolderAccessMode.ReadWrite);
        model.Grants.Single().Scope.ShouldBe(FolderGrantScope.Global);
        model.Revoke("/work");
        model.Grants.ShouldBeEmpty();
    }

    private sealed class FakeFolderGrantSource : IFolderGrantSource
    {
        private readonly List<FolderGrantDraft> _grants = [];

        public IReadOnlyList<FolderGrantDraft> List() => _grants;

        public void Grant(FolderGrantDraft grant)
        {
            _grants.RemoveAll(item => string.Equals(item.Path, grant.Path, StringComparison.Ordinal));
            _grants.Add(grant);
        }

        public void Revoke(string path) => _grants.RemoveAll(item => string.Equals(item.Path, path, StringComparison.Ordinal));
    }
}
