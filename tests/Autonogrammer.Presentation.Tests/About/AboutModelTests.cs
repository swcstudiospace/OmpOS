using Autonogrammer.Presentation.About;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.About;

/// <summary>The About model: the version and revision of a build, the source-offer links, the copied text and the announcements.</summary>
public sealed class AboutModelTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The version is what precedes the build metadata, whatever follows the first plus sign.</summary>
    [Theory]
    [InlineData("0.15.0+0123456789abcdef0123456789abcdef01234567", "0.15.0")]
    [InlineData("0.15.0", "0.15.0")]
    [InlineData("0.15.0-rc.1+ABCDEF1.dirty", "0.15.0-rc.1")]
    [InlineData("  0.15.0+abcdef1  ", "0.15.0")]
    [InlineData("0.15.0+not-a-sha", "0.15.0")]
    [InlineData("+abcdef1", "unknown")]
    [InlineData("", "unknown")]
    [InlineData("   ", "unknown")]
    [InlineData(null, "unknown")]
    public void TheVersionIsEverythingBeforeTheBuildMetadata(string? informational, string expected)
    {
        AboutModel.VersionOf(informational).ShouldBe(expected);
    }

    /// <summary>The revision is the first dot-separated part of the build metadata when it is 7 to 64 hexadecimal digits; anything else is unknown.</summary>
    [Theory]
    [InlineData("0.15.0+0123456789abcdef0123456789abcdef01234567", "0123456789abcdef0123456789abcdef01234567")]
    [InlineData("0.15.0-rc.1+ABCDEF1.dirty", "abcdef1")]
    [InlineData("0.15.0+abcdef1", "abcdef1")]
    [InlineData("0.15.0+abcdef", "unknown")]
    [InlineData("0.15.0+abcdefg", "unknown")]
    [InlineData("0.15.0+not-a-sha", "unknown")]
    [InlineData("0.15.0+", "unknown")]
    [InlineData("0.15.0", "unknown")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void TheRevisionIsTheHexadecimalBuildMetadataOrUnknown(string? informational, string expected)
    {
        AboutModel.RevisionOf(informational).ShouldBe(expected);
    }

    /// <summary>The model shows the licence, the licence address and the source address of ADR-0018, and the notice files in reading order.</summary>
    [Fact]
    public void TheModelCarriesTheSourceOfferFields()
    {
        var model = AboutModel.Create("0.15.0+" + Sha, string.Empty);

        model.ProductName.ShouldBe("Autonogrammer");
        model.Version.ShouldBe("0.15.0");
        model.Revision.ShouldBe(Sha);
        model.Licence.ShouldBe("AGPL-3.0-or-later");
        model.LicenceLocation.ShouldBe("https://www.gnu.org/licenses/agpl-3.0.html");
        model.SourceLocation.ShouldBe("https://github.com/swcstudiospace/OmpOS");
        model.NoticeFiles.ShouldBe(["LICENSE", "NOTICE", "THIRD-PARTY-NOTICES.md"]);
        model.SourceOffer.ShouldContain("no charge", Case.Sensitive);
        model.SourceOffer.ShouldContain("GNU Affero General Public License", Case.Sensitive);
    }

    /// <summary>The links lead to the source tree, the licence text and each notice file at the revision of the build.</summary>
    [Fact]
    public void TheLinksPointAtTheBuildsRevision()
    {
        var model = AboutModel.Create("0.15.0+" + Sha, string.Empty);

        model.Links.Select(static link => link.Location).ShouldBe(
        [
            "https://github.com/swcstudiospace/OmpOS",
            "https://www.gnu.org/licenses/agpl-3.0.html",
            "https://github.com/swcstudiospace/OmpOS/blob/" + Sha + "/LICENSE",
            "https://github.com/swcstudiospace/OmpOS/blob/" + Sha + "/NOTICE",
            "https://github.com/swcstudiospace/OmpOS/blob/" + Sha + "/THIRD-PARTY-NOTICES.md",
        ]);
        model.Links.ShouldAllBe(static link => Uri.IsWellFormedUriString(link.Location, UriKind.Absolute) && link.Location.StartsWith("https://", StringComparison.Ordinal));
        model.Links.ShouldAllBe(static link => link.AccessibleName.StartsWith(link.Label, StringComparison.Ordinal) && link.AccessibleName.Length > link.Label.Length);
    }

    /// <summary>A build that recorded no revision links the notice files on the default branch, and says the revision is unknown.</summary>
    [Fact]
    public void AnUnknownRevisionLinksTheDefaultBranch()
    {
        var model = AboutModel.Create("0.15.0", string.Empty);

        model.Revision.ShouldBe("unknown");
        model.Links[2].Location.ShouldBe("https://github.com/swcstudiospace/OmpOS/blob/main/LICENSE");
    }

    /// <summary>The copied text names the product, version, revision, licence with its address, the source address and, when given, the platform line.</summary>
    [Fact]
    public void TheVersionInfoTextHasOneLinePerFact()
    {
        var withPlatform = AboutModel.Create("0.15.0+abcdef1", "Linux 6.8 (X64), .NET 10.0.0");
        var without = AboutModel.Create("0.15.0", string.Empty);

        withPlatform.VersionInfoText.ShouldBe(
            "Autonogrammer 0.15.0\n"
            + "Revision: abcdef1\n"
            + "Licence: AGPL-3.0-or-later (https://www.gnu.org/licenses/agpl-3.0.html)\n"
            + "Source code: https://github.com/swcstudiospace/OmpOS\n"
            + "Linux 6.8 (X64), .NET 10.0.0");
        without.VersionInfoText.ShouldBe(
            "Autonogrammer 0.15.0\n"
            + "Revision: unknown\n"
            + "Licence: AGPL-3.0-or-later (https://www.gnu.org/licenses/agpl-3.0.html)\n"
            + "Source code: https://github.com/swcstudiospace/OmpOS");
    }

    /// <summary>Copy version info writes exactly the version text to the clipboard and announces it.</summary>
    [Fact]
    public async Task CopyingWritesTheVersionTextAndAnnouncesIt()
    {
        var model = AboutModel.Create("0.15.0+abcdef1", string.Empty);
        var clipboard = new FakeAboutClipboard();

        var copied = await model.CopyVersionInfoAsync(clipboard, TestContext.Current.CancellationToken).ConfigureAwait(true);

        copied.ShouldBeTrue();
        clipboard.Texts.ShouldBe([model.VersionInfoText]);
        model.Status.ShouldBe("Version information copied.");
    }

    /// <summary>A clipboard that refuses the text gives a failure announcement and a false result; nothing throws.</summary>
    [Fact]
    public async Task ARefusedCopyIsAnnouncedAsAFailure()
    {
        var model = AboutModel.Create("0.15.0", string.Empty);
        var clipboard = new FakeAboutClipboard { Refuse = true };

        var copied = await model.CopyVersionInfoAsync(clipboard, TestContext.Current.CancellationToken).ConfigureAwait(true);

        copied.ShouldBeFalse();
        clipboard.Texts.ShouldBeEmpty();
        model.Status.ShouldBe("Version information could not be copied.");
    }

    /// <summary>A second copy changes the status again (to empty, then to the result), so a live region announces it a second time.</summary>
    [Fact]
    public async Task ARepeatedCopyChangesTheStatusAgain()
    {
        var model = AboutModel.Create("0.15.0", string.Empty);
        var clipboard = new FakeAboutClipboard();
        var seen = new List<string>();
        model.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(AboutModel.Status), StringComparison.Ordinal))
            {
                seen.Add(model.Status);
            }
        };

        _ = await model.CopyVersionInfoAsync(clipboard, TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = await model.CopyVersionInfoAsync(clipboard, TestContext.Current.CancellationToken).ConfigureAwait(true);

        seen.ShouldBe(["Version information copied.", string.Empty, "Version information copied."]);
    }

    /// <summary>A cancelled copy throws and leaves the clipboard untouched.</summary>
    [Fact]
    public async Task ACancelledCopyThrows()
    {
        var model = AboutModel.Create("0.15.0", string.Empty);
        var clipboard = new CancellingClipboard();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await model.CopyVersionInfoAsync(clipboard, TestContext.Current.CancellationToken).ConfigureAwait(true)).ConfigureAwait(true);
    }

    /// <summary>A link that cannot be opened is announced by its label.</summary>
    [Fact]
    public void AnAddressThatCannotBeOpenedIsAnnounced()
    {
        var model = AboutModel.Create("0.15.0", string.Empty);

        model.ReportOpenFailed(model.Links[0]);

        model.Status.ShouldStartWith(model.Links[0].Label, Case.Sensitive);
        model.Status.ShouldContain("could not be opened", Case.Sensitive);
    }

    /// <summary>The model of this build's own assembly has a real version (VersionPrefix) and no build metadata in it.</summary>
    [Fact]
    public void TheModelOfARealAssemblyHasAVersionWithoutMetadata()
    {
        var model = AboutModel.ForAssembly(typeof(AboutModel).Assembly, string.Empty);

        model.Version.ShouldNotBe("unknown", StringComparer.Ordinal);
        model.Version.ShouldNotContain("+", Case.Sensitive);
    }

    /// <summary>Null arguments are refused.</summary>
    [Fact]
    public void NullArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => new AboutModel(null!, "unknown", string.Empty));
        Should.Throw<ArgumentNullException>(() => new AboutModel("0.15.0", null!, string.Empty));
        Should.Throw<ArgumentNullException>(() => new AboutModel("0.15.0", "unknown", null!));
        Should.Throw<ArgumentNullException>(() => AboutModel.ForAssembly(null!, string.Empty));
        Should.Throw<ArgumentNullException>(() => AboutModel.Create("0.15.0", null!));
        Should.Throw<ArgumentNullException>(() => AboutModel.Create("0.15.0", string.Empty).ReportOpenFailed(null!));
    }
}
