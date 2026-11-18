using Autonogrammer.Presentation.Settings.Permissions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>Saving a folder grant returns the grant as stored, and a source that keeps the folder under another spelling does not fail the save.</summary>
public sealed class FolderGrantModelTests
{
    /// <summary>A source that stores the folder in upper case still saves: the save returns the grant as requested and throws nothing.</summary>
    [Fact]
    public void ASourceThatRespellsTheFolderDoesNotFailTheSave()
    {
        var source = new RespellingGrants();
        var model = new FolderGrantModel(source);

        var saved = model.Save("/work/Repo", FolderAccessMode.ReadWrite, FolderGrantScope.Project);

        saved.ShouldBe(new FolderGrantDraft("/work/Repo", FolderAccessMode.ReadWrite, FolderGrantScope.Project));
        source.List().ShouldHaveSingleItem().Path.ShouldBe("/WORK/REPO");
    }

    private sealed class RespellingGrants : IFolderGrantSource
    {
        private readonly List<FolderGrantDraft> _grants = [];

        public IReadOnlyList<FolderGrantDraft> List() => _grants;

        public void Grant(FolderGrantDraft grant) => _grants.Add(grant with { Path = grant.Path.ToUpperInvariant() });

        public void Revoke(string path) => _grants.RemoveAll(grant => string.Equals(grant.Path, path, StringComparison.Ordinal));
    }
}
