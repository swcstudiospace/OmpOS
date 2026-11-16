using Autonogrammer.Presentation.Landing;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Landing;

/// <summary>The Local machine card for the managed omp: the states that offer an install, the install's progress and retry, and the update notices.</summary>
public sealed class CodeLandingLocalOmpTests
{
    private static readonly DateTimeOffset CheckedAt = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the notices and the text each must add to the Omp line.</summary>
    public static TheoryData<LocalOmpUpdate, string> Notices => new()
    {
        { new LocalOmpUpdate(LocalOmpUpdateState.Updated, "2.0.0", null, "Installed."), "Omp 1.2.3 from the managed install. Omp 2.0.0 is now the managed install." },
        { new LocalOmpUpdate(LocalOmpUpdateState.UpToDate, "1.2.3", CheckedAt, "Up to date."), "Omp 1.2.3 from the managed install." },
        { new LocalOmpUpdate(LocalOmpUpdateState.None, null, null, null), "Omp 1.2.3 from the managed install." },
        { new LocalOmpUpdate(LocalOmpUpdateState.Offline, "1.2.3", CheckedAt, "Offline."), "Omp 1.2.3 from the managed install. Could not reach the registry to check for a newer Omp. Last checked 2026-11-07 12:00 UTC." },
        { new LocalOmpUpdate(LocalOmpUpdateState.Offline, "1.2.3", null, "Offline."), "Omp 1.2.3 from the managed install. Could not reach the registry to check for a newer Omp." },
        { new LocalOmpUpdate(LocalOmpUpdateState.Deferred, "2.0.0", null, "In use."), "Omp 1.2.3 from the managed install. A newer Omp will install on the next launch, because the current Omp is in use." },
        { new LocalOmpUpdate(LocalOmpUpdateState.RolledBack, "1.2.3", null, "Probe failed."), "Omp 1.2.3 from the managed install. A newer Omp failed its check and was rolled back. Probe failed." },
        { new LocalOmpUpdate(LocalOmpUpdateState.Failed, null, null, "Disk full."), "Omp 1.2.3 from the managed install. Omp could not be updated: Disk full." },
    };

    /// <summary>The Install Omp action is offered only when omp is not installed or the managed install is missing.</summary>
    /// <param name="state">The state of omp.</param>
    /// <param name="offersInstall">Whether the card must carry an action labelled Install Omp.</param>
    [Theory]
    [InlineData(LocalOmpState.NotInstalled, true)]
    [InlineData(LocalOmpState.Missing, true)]
    [InlineData(LocalOmpState.Installed, false)]
    [InlineData(LocalOmpState.Failed, false)]
    [InlineData(LocalOmpState.NotChecked, false)]
    [InlineData(LocalOmpState.Pending, false)]
    [InlineData(LocalOmpState.Installing, false)]
    [InlineData(LocalOmpState.InstallFailed, false)]
    public void TheInstallActionAppearsOnlyWhenOmpIsNotInstalledOrTheManagedInstallIsMissing(LocalOmpState state, bool offersInstall)
    {
        var card = Local(StatusOf(state));

        card.Actions.Any(action => string.Equals(action.Label, CodeLandingCopy.InstallOmp, StringComparison.Ordinal)).ShouldBe(offersInstall);
    }

    /// <summary>A runnable omp without the managed install says so, keeps the card ready and offers the install.</summary>
    [Fact]
    public void AMissingManagedInstallSaysSoAndOffersTheInstall()
    {
        var card = Local(LocalOmpStatus.Missing("1.2.3", LocalOmpSource.Path));

        card.State.ShouldBe(CodeLandingCardState.Ready);
        card.Message.ShouldBe(CodeLandingCopy.LocalMissing);
        card.OmpText.ShouldBe("Omp 1.2.3 from PATH.");
        card.Primary!.IsEnabled.ShouldBeTrue();
        var install = card.Actions.ShouldHaveSingleItem();
        install.Label.ShouldBe(CodeLandingCopy.InstallOmp);
        install.Action.ShouldBe(CodeLandingAction.InstallOmp);
        install.AccessibleName.ShouldBe(CodeLandingCopy.InstallOmp + ", " + CodeLandingCopy.LocalTitle);
    }

    /// <summary>An omp that is not installed gives the guidance and offers the install.</summary>
    [Fact]
    public void ANotInstalledOmpGivesTheGuidanceAndOffersTheInstall()
    {
        var card = Local(LocalOmpStatus.NotInstalled("Install omp, then put it on PATH."));

        card.State.ShouldBe(CodeLandingCardState.Unavailable);
        card.OmpText.ShouldBe("Omp is not installed. Install omp, then put it on PATH.");
        var install = card.Actions.ShouldHaveSingleItem();
        install.Label.ShouldBe(CodeLandingCopy.InstallOmp);
        install.Action.ShouldBe(CodeLandingAction.InstallOmp);
    }

    /// <summary>An install in its first stage says it reads the release, and offers Cancel while the card's primary button is disabled.</summary>
    [Fact]
    public void AnInstallReadingTheReleaseShowsItsStageAndOffersCancel()
    {
        var card = Local(LocalOmpStatus.Installing(LocalOmpInstallStage.ReadingRelease, null));

        card.State.ShouldBe(CodeLandingCardState.Installing);
        card.Message.ShouldBe(CodeLandingCopy.LocalInstalling);
        card.OmpText.ShouldBe(CodeLandingCopy.ProgressReadingRelease);
        card.Primary!.IsEnabled.ShouldBeFalse("a session must not start on an omp that is being replaced");
        var cancel = card.Actions.ShouldHaveSingleItem();
        cancel.Label.ShouldBe(CodeLandingCopy.CancelInstall);
        cancel.Action.ShouldBe(CodeLandingAction.CancelOmpInstall);
    }

    /// <summary>An install that is installing names the version it is installing.</summary>
    [Fact]
    public void AnInstallThatKnowsItsVersionNamesIt()
    {
        var card = Local(LocalOmpStatus.Installing(LocalOmpInstallStage.Installing, "2.0.0"));

        card.State.ShouldBe(CodeLandingCardState.Installing);
        card.OmpText.ShouldBe("Installing Omp 2.0.0.");
    }

    /// <summary>An install whose version is not known yet says only that it is installing.</summary>
    [Fact]
    public void AnInstallWithoutAKnownVersionSaysItIsInstalling()
    {
        var card = Local(LocalOmpStatus.Installing(LocalOmpInstallStage.Installing, null));

        card.OmpText.ShouldBe(CodeLandingCopy.ProgressInstallingUnknown);
    }

    /// <summary>An install that did not complete shows its reason and a Try again action that installs again.</summary>
    [Fact]
    public void AFailedInstallShowsItsReasonAndATryAgainAction()
    {
        var card = Local(LocalOmpStatus.InstallFailed("The download does not match its digest."));

        card.State.ShouldBe(CodeLandingCardState.Failed);
        card.Message.ShouldBe(CodeLandingCopy.LocalInstallFailed);
        card.OmpText.ShouldBe("Omp could not be installed: The download does not match its digest.");
        var retry = card.Actions.ShouldHaveSingleItem();
        retry.Label.ShouldBe(CodeLandingCopy.Retry);
        retry.Action.ShouldBe(CodeLandingAction.InstallOmp);
        retry.AccessibleName.ShouldBe(CodeLandingCopy.Retry + ", " + CodeLandingCopy.LocalTitle);
    }

    /// <summary>Each update outcome adds its own notice to the Omp line, and an up-to-date or unknown outcome adds none.</summary>
    /// <param name="update">The outcome of the check.</param>
    /// <param name="expected">The Omp line the card must show.</param>
    [Theory]
    [MemberData(nameof(Notices))]
    public void EachUpdateOutcomeAddsItsNotice(LocalOmpUpdate update, string expected)
    {
        var card = Local(LocalOmpStatus.Installed("1.2.3", LocalOmpSource.Managed), update);

        card.OmpText.ShouldBe(expected);
    }

    /// <summary>A check that finishes after the status is shown updates the same card without rebuilding the rest of the landing.</summary>
    [Fact]
    public void ASetUpdateRedrawsTheLocalCard()
    {
        var model = new CodeLandingModel(new NewSessionTargetModel([]));
        model.SetLocal(LocalOmpStatus.Installed("1.2.3", LocalOmpSource.Managed));
        model.Cards[0].OmpText.ShouldBe("Omp 1.2.3 from the managed install.");

        model.SetLocalUpdate(new LocalOmpUpdate(LocalOmpUpdateState.Updated, "2.0.0", null, null));

        model.Cards[0].OmpText.ShouldBe("Omp 1.2.3 from the managed install. Omp 2.0.0 is now the managed install.");
        model.Cards.Count.ShouldBe(4);
    }

    /// <summary>The Local card's primary button is disabled while an install runs, so a session cannot start on the omp being replaced.</summary>
    [Fact]
    public void TheLocalCardIsEnabledOutsideAnInstall()
    {
        Local(LocalOmpStatus.Installed("1.2.3", LocalOmpSource.Managed)).Primary!.IsEnabled.ShouldBeTrue();
    }

    private static CodeLandingCard Local(LocalOmpStatus status, LocalOmpUpdate? update = null) =>
        CodeLandingModel.Build(new CodeLandingInputs(status, RemoteLoadState.Loaded, null, null, update), [])[0];

    private static LocalOmpStatus StatusOf(LocalOmpState state) => state switch
    {
        LocalOmpState.NotInstalled => LocalOmpStatus.NotInstalled("Install omp."),
        LocalOmpState.Missing => LocalOmpStatus.Missing("1.2.3", LocalOmpSource.Path),
        LocalOmpState.Installed => LocalOmpStatus.Installed("1.2.3", LocalOmpSource.Managed),
        LocalOmpState.Failed => LocalOmpStatus.Failed("disk on fire"),
        LocalOmpState.NotChecked => LocalOmpStatus.NotChecked,
        LocalOmpState.Installing => LocalOmpStatus.Installing(LocalOmpInstallStage.Installing, "2.0.0"),
        LocalOmpState.InstallFailed => LocalOmpStatus.InstallFailed("Failed."),
        _ => LocalOmpStatus.Pending,
    };
}
