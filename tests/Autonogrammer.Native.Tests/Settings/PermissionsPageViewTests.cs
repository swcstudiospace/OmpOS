#pragma warning disable MA0002
#pragma warning disable MA0006
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings.Permissions;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The Permissions view renders modes, folder grants and limit statements with accessible names.</summary>
public sealed class PermissionsPageViewTests
{
    [Fact]
    public async Task RendersModesGrantsAndStatementsWithAccessibleNames()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var source = new MemoryGrants();
            source.Grant(new FolderGrantDraft("/work", FolderAccessMode.ReadWrite, FolderGrantScope.Session));
            var view = new PermissionsPageView(new PermissionModesModel(), new FolderGrantModel(source), new PermissionStatementModel());
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);
            try
            {
                SettingsTree.Texts(view).ShouldContain("Auto (rules)");
                SettingsTree.Texts(view).ShouldContain("Manual");
                SettingsTree.Texts(view).ShouldContain(PermissionModesModel.SkipWarning);
                SettingsTree.Texts(view).ShouldContain("/work ReadWrite Session");
                SettingsTree.Texts(view).ShouldContain(PermissionStatementModel.DeleteProtectionAlwaysAsks);
                SettingsTree.Has<TextBlock>(view, "Permission mode").ShouldBeTrue();
                SettingsTree.Has<TextBlock>(view, "Folder grants").ShouldBeTrue();
                SettingsTree.Has<TextBlock>(view, "Limit statements").ShouldBeTrue();
                SettingsTree.Has<TextBlock>(view, "Skip warning").ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }, ct);
    }

    private sealed class MemoryGrants : IFolderGrantSource
    {
        private readonly List<FolderGrantDraft> _grants = [];

        public IReadOnlyList<FolderGrantDraft> List() => _grants;

        public void Grant(FolderGrantDraft grant) => _grants.Add(grant);

        public void Revoke(string path) => _grants.RemoveAll(item => item.Path == path);
    }
}

#pragma warning restore MA0002
#pragma warning restore MA0006
