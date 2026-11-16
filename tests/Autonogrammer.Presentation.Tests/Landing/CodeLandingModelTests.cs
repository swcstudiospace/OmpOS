using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Accounts;
using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.Presentation.Landing;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Landing;

/// <summary>The Code landing: the four cards in their fixed order, the state, message, Omp status, enabled flags and next actions each input gives, and the model's notices.</summary>
public sealed class CodeLandingModelTests
{
    private const string Version = "1.2.3";

    /// <summary>The four cards are always this computer, a linked machine, cloud and remote, in that order.</summary>
    [Fact]
    public void TheCardsAreInTheFixedOrder()
    {
        var cards = Cards(Inputs());

        cards.Select(static card => card.Kind).ShouldBe([CodeLandingCardKind.LocalMachine, CodeLandingCardKind.LinkedMachine, CodeLandingCardKind.Cloud, CodeLandingCardKind.Remote]);
        cards.Select(static card => card.Title).ShouldBe([CodeLandingCopy.LocalTitle, CodeLandingCopy.LinkedTitle, CodeLandingCopy.CloudTitle, CodeLandingCopy.RemoteTitle]);
    }

    /// <summary>A fresh landing has nothing checked yet: this computer is checking and the remote list is loading.</summary>
    [Fact]
    public void AFreshLandingIsCheckingAndLoading()
    {
        var model = new CodeLandingModel(Targets());

        model.Cards[0].State.ShouldBe(CodeLandingCardState.Checking);
        model.Cards[0].OmpText.ShouldBe(CodeLandingCopy.OmpChecking);
        model.Cards[3].State.ShouldBe(CodeLandingCardState.Checking);
        model.Cards[3].Message.ShouldBe(RemoteCopy.Loading);
    }

    /// <summary>An installed omp shows its version and where it came from, and this computer starts an agent here.</summary>
    [Theory]
    [InlineData(LocalOmpSource.Managed, "the managed install")]
    [InlineData(LocalOmpSource.Configured, "the configured path")]
    [InlineData(LocalOmpSource.Path, "PATH")]
    public void AnInstalledOmpShowsItsVersionAndSource(LocalOmpSource source, string from)
    {
        var card = Card(Cards(Inputs(LocalOmpStatus.Installed(Version, source))), CodeLandingCardKind.LocalMachine);

        card.State.ShouldBe(CodeLandingCardState.Ready);
        card.OmpText.ShouldBe("Omp 1.2.3 from " + from + ".");
        card.Primary.ShouldNotBeNull();
        card.Primary.IsEnabled.ShouldBeTrue();
        card.Primary.Action.ShouldBe(CodeLandingAction.StartOnEnvironment);
        card.Primary.OptionId.ShouldBe(NewSessionTargetModel.LocalId);
    }

    /// <summary>When the version probe could not read a version, the status still says where omp was found.</summary>
    [Fact]
    public void AnOmpWithoutAVersionSaysItWasFound()
    {
        var card = Card(Cards(Inputs(LocalOmpStatus.Installed(version: null, LocalOmpSource.Path))), CodeLandingCardKind.LocalMachine);

        card.OmpText.ShouldBe("Omp found from PATH.");
    }

    /// <summary>A missing omp says so, gives the guidance, and still leaves this computer enabled.</summary>
    [Fact]
    public void AMissingOmpSaysItIsNotInstalledAndGivesTheGuidance()
    {
        var card = Card(Cards(Inputs(LocalOmpStatus.NotInstalled("Install omp, then put it on PATH."))), CodeLandingCardKind.LocalMachine);

        card.State.ShouldBe(CodeLandingCardState.Unavailable);
        card.OmpText.ShouldBe("Omp is not installed. Install omp, then put it on PATH.");
        card.Primary.ShouldNotBeNull();
        card.Primary.IsEnabled.ShouldBeTrue();
    }

    /// <summary>A check that threw shows the reason and offers a retry.</summary>
    [Fact]
    public void AFailedOmpCheckShowsTheReasonAndOffersARetry()
    {
        var card = Card(Cards(Inputs(LocalOmpStatus.Failed("disk on fire"))), CodeLandingCardKind.LocalMachine);

        card.State.ShouldBe(CodeLandingCardState.Failed);
        card.OmpText.ShouldBe("Omp could not be checked: disk on fire");
        card.Actions.ShouldHaveSingleItem().Action.ShouldBe(CodeLandingAction.RetryOmpCheck);
        card.Actions[0].Label.ShouldBe(CodeLandingCopy.Retry);
    }

    /// <summary>A window with no omp check says so instead of inventing a status.</summary>
    [Fact]
    public void AWindowWithoutAnOmpCheckSaysItIsNotChecked()
    {
        var card = Card(Cards(Inputs(LocalOmpStatus.NotChecked)), CodeLandingCardKind.LocalMachine);

        card.State.ShouldBe(CodeLandingCardState.Unavailable);
        card.Message.ShouldBe(CodeLandingCopy.LocalNotChecked);
        card.OmpText.ShouldBe(CodeLandingCopy.OmpNotChecked);
    }

    /// <summary>The linked-machine card is disabled in every state.</summary>
    [Theory]
    [InlineData(LinkedCase.SignedOut)]
    [InlineData(LinkedCase.NoAccount)]
    [InlineData(LinkedCase.NotConfigured)]
    [InlineData(LinkedCase.Offline)]
    [InlineData(LinkedCase.NoSource)]
    [InlineData(LinkedCase.NoMachines)]
    [InlineData(LinkedCase.Machines)]
    public void TheLinkedMachineCardIsDisabledInEveryState(LinkedCase linkedCase)
    {
        var card = Card(Cards(LinkedInputs(linkedCase)), CodeLandingCardKind.LinkedMachine);

        card.Primary.ShouldNotBeNull();
        card.Primary.IsEnabled.ShouldBeFalse();
        card.Primary.Action.ShouldBeNull();
    }

    /// <summary>Signed out, the linked-machine card says to sign in, and the next action signs in.</summary>
    [Fact]
    public void ASignedOutAccountAsksToSignInToLinkAMachine()
    {
        var card = Card(Cards(Inputs(account: Account(AuthPhase.SignedOut))), CodeLandingCardKind.LinkedMachine);

        card.State.ShouldBe(CodeLandingCardState.SignedOut);
        card.Message.ShouldBe("Sign in to link a machine");
        card.Actions.ShouldHaveSingleItem().Action.ShouldBe(CodeLandingAction.SignIn);
    }

    /// <summary>Signed in with no linked machines, the card says there are none.</summary>
    [Fact]
    public void ASignedInAccountWithNoLinkedMachinesSaysSo()
    {
        var card = Card(Cards(Inputs(account: SignedIn(), linked: [])), CodeLandingCardKind.LinkedMachine);

        card.State.ShouldBe(CodeLandingCardState.Empty);
        card.Message.ShouldBe("No linked machines yet");
        card.Rows.ShouldBeEmpty();
    }

    /// <summary>Without a source of linked machines the card says that, and does not claim there are none.</summary>
    [Fact]
    public void WithoutALinkedMachineSourceTheCardSaysItIsNotSetUp()
    {
        var card = Card(Cards(Inputs(account: SignedIn(), linked: null)), CodeLandingCardKind.LinkedMachine);

        card.State.ShouldBe(CodeLandingCardState.Unavailable);
        card.Message.ShouldBe(DispatchCopy.NotConfigured);
    }

    /// <summary>Linked machines are listed as disabled names, with the note that starting one is not offered yet.</summary>
    [Fact]
    public void LinkedMachinesAreListedAsDisabledNames()
    {
        var card = Card(Cards(Inputs(account: SignedIn(), linked: ["laptop", "tower"])), CodeLandingCardKind.LinkedMachine);

        card.State.ShouldBe(CodeLandingCardState.Ready);
        card.Message.ShouldBe(CodeLandingCopy.LinkedListed);
        card.Rows.Select(static row => row.Label).ShouldBe(["laptop", "tower"]);
        card.Rows.All(static row => !row.IsEnabled && row.Action is null).ShouldBeTrue();
    }

    /// <summary>When the service cannot be reached the linked card says so.</summary>
    [Fact]
    public void AnOfflineAccountSaysTheServiceCannotBeReached()
    {
        var card = Card(Cards(Inputs(account: SignedIn(offline: true), linked: ["laptop"])), CodeLandingCardKind.LinkedMachine);

        card.State.ShouldBe(CodeLandingCardState.Offline);
        card.Message.ShouldBe(AccountCopy.OfflineNotice);
        card.Actions.ShouldBeEmpty();
    }

    /// <summary>Cloud is disabled when the account reports no sign-in configuration, and when there is no account host at all.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CloudIsDisabledWhenSignInIsNotConfigured(bool hasAccount)
    {
        var account = hasAccount ? Account(AuthPhase.SignedIn, configured: false) : null;

        var card = Card(Cards(Inputs(account: account)), CodeLandingCardKind.Cloud);

        card.State.ShouldBe(CodeLandingCardState.NotConfigured);
        card.Message.ShouldBe(AccountCopy.NotConfigured);
        card.Primary.ShouldNotBeNull();
        card.Primary.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Signed out, cloud says to sign in to use cloud agents, and the next action signs in.</summary>
    [Fact]
    public void ASignedOutAccountAsksToSignInForCloud()
    {
        var card = Card(Cards(Inputs(account: Account(AuthPhase.SignedOut))), CodeLandingCardKind.Cloud);

        card.State.ShouldBe(CodeLandingCardState.SignedOut);
        card.Message.ShouldBe("Sign in to use cloud agents");
        card.Primary!.IsEnabled.ShouldBeFalse();
        card.Actions.ShouldHaveSingleItem().Action.ShouldBe(CodeLandingAction.SignIn);
    }

    /// <summary>Cloud is disabled while the service cannot be reached, even though the account is signed in.</summary>
    [Fact]
    public void CloudIsDisabledWhileTheServiceIsOffline()
    {
        var card = Card(Cards(Inputs(account: SignedIn(offline: true))), CodeLandingCardKind.Cloud);

        card.State.ShouldBe(CodeLandingCardState.Offline);
        card.Message.ShouldBe(AccountCopy.OfflineNotice);
        card.Primary!.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Signed in and configured, cloud is enabled and its button opens the cloud page.</summary>
    [Fact]
    public void CloudIsEnabledWhenSignedInAndConfigured()
    {
        var card = Card(Cards(Inputs(account: SignedIn())), CodeLandingCardKind.Cloud);

        card.State.ShouldBe(CodeLandingCardState.Ready);
        card.Message.ShouldBe(CodeLandingCopy.CloudReady);
        card.Primary!.IsEnabled.ShouldBeTrue();
        card.Primary.Action.ShouldBe(CodeLandingAction.OpenCloud);
    }

    /// <summary>While the saved environments load, the remote card says so and offers nothing to press.</summary>
    [Fact]
    public void RemoteIsLoadingUntilTheListIsRead()
    {
        var card = Card(Cards(Inputs(remote: RemoteLoadState.Loading)), CodeLandingCardKind.Remote);

        card.State.ShouldBe(CodeLandingCardState.Checking);
        card.Message.ShouldBe(RemoteCopy.Loading);
        card.Primary.ShouldBeNull();
        card.Rows.ShouldBeEmpty();
        card.Actions.ShouldBeEmpty();
    }

    /// <summary>When the list cannot be read the remote card shows the failure and offers a retry.</summary>
    [Fact]
    public void ARemoteLoadFailureOffersARetry()
    {
        var card = Card(Cards(Inputs(remote: RemoteLoadState.Failed)), CodeLandingCardKind.Remote);

        card.State.ShouldBe(CodeLandingCardState.Failed);
        card.Message.ShouldBe(RemoteCopy.LoadFailed);
        card.Actions.ShouldHaveSingleItem().Action.ShouldBe(CodeLandingAction.RetryRemoteLoad);
        card.Actions[0].Label.ShouldBe(CodeLandingCopy.Retry);
    }

    /// <summary>With no remote environments the card says so, and its button opens Remote environments in Settings.</summary>
    [Fact]
    public void WithNoRemoteEnvironmentsTheCardOpensSettings()
    {
        var card = Card(Cards(Inputs(remote: RemoteLoadState.Loaded), Targets()), CodeLandingCardKind.Remote);

        card.State.ShouldBe(CodeLandingCardState.Empty);
        card.Message.ShouldBe("No remote environments yet");
        card.Rows.ShouldBeEmpty();
        var manage = card.Actions.ShouldHaveSingleItem();
        manage.Action.ShouldBe(CodeLandingAction.ManageRemote);
        manage.Label.ShouldBe(CodeLandingCopy.ManageRemote);
    }

    /// <summary>The remote card lists the ssh and container environments of the picker, and each starts a session on its own identifier.</summary>
    [Fact]
    public void TheRemoteCardListsTheSshAndContainerEnvironments()
    {
        var targets = Targets(Ssh("box-1"), Container("dev-1"));

        var card = Card(Cards(Inputs(remote: RemoteLoadState.Loaded), targets), CodeLandingCardKind.Remote);

        card.State.ShouldBe(CodeLandingCardState.Ready);
        card.Rows.Select(static row => row.OptionId).ShouldBe(["ssh:box-1", "container:dev-1"]);
        card.Rows.All(static row => row.IsEnabled && row.Action == CodeLandingAction.StartOnEnvironment).ShouldBeTrue();
        card.Actions.ShouldHaveSingleItem().Action.ShouldBe(CodeLandingAction.ManageRemote);
    }

    /// <summary>An environment whose last test failed is listed but cannot be chosen.</summary>
    [Fact]
    public void AnEnvironmentThatIsBlockedIsListedButDisabled()
    {
        var targets = Targets(Ssh("box-1"), Ssh("box-2"));
        targets.SetStatus("box-2", RemoteEnvironmentStatus.AuthFailed);

        var card = Card(Cards(Inputs(remote: RemoteLoadState.Loaded), targets), CodeLandingCardKind.Remote);

        card.Rows.Single(static row => string.Equals(row.OptionId, "ssh:box-1", StringComparison.Ordinal)).IsEnabled.ShouldBeTrue();
        card.Rows.Single(static row => string.Equals(row.OptionId, "ssh:box-2", StringComparison.Ordinal)).IsEnabled.ShouldBeFalse();
    }

    /// <summary>The Omp status of the remote, linked and cloud cards is not checked yet: the remote check is a separate unit.</summary>
    [Fact]
    public void TheOtherCardsSayTheirOmpIsNotCheckedYet()
    {
        var cards = Cards(Inputs(account: SignedIn(), remote: RemoteLoadState.Loaded, linked: ["laptop"]), Targets(Ssh("box-1")));

        cards.Where(static card => card.Kind != CodeLandingCardKind.LocalMachine)
            .Select(static card => card.OmpText)
            .ShouldAllBe(static text => text == CodeLandingCopy.OmpNotChecked);
    }

    /// <summary>A card's accessible name says its title, its state and its Omp status, in that order.</summary>
    [Fact]
    public void ACardsAccessibleNameSaysTitleStateAndOmp()
    {
        var card = Card(Cards(Inputs(LocalOmpStatus.Installed(Version, LocalOmpSource.Path))), CodeLandingCardKind.LocalMachine);

        card.AccessibleName.ShouldBe("Local machine. Ready on this computer. Omp 1.2.3 from PATH.");
    }

    /// <summary>A next action's accessible name carries the card it belongs to, so two sign-in buttons are told apart.</summary>
    [Fact]
    public void ANextActionNameCarriesItsCard()
    {
        var card = Card(Cards(Inputs(account: Account(AuthPhase.SignedOut))), CodeLandingCardKind.Cloud);

        card.Actions[0].AccessibleName.ShouldBe("Sign in, Cloud");
    }

    /// <summary>A new landing shows checking and loading, and each input replaces the card it draws.</summary>
    [Fact]
    public void SettingAnInputRedrawsItsCard()
    {
        var model = new CodeLandingModel(Targets());
        var changed = new List<string?>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        model.SetLocal(LocalOmpStatus.NotInstalled("Install omp."));
        model.SetRemote(RemoteLoadState.Loaded);

        model.Cards[0].State.ShouldBe(CodeLandingCardState.Unavailable);
        model.Cards[3].State.ShouldBe(CodeLandingCardState.Empty);
        changed.ShouldBe([nameof(CodeLandingModel.Cards), nameof(CodeLandingModel.Cards)]);
    }

    /// <summary>Changing the saved environments redraws the remote rows, so the card and the picker agree.</summary>
    [Fact]
    public void SavedEnvironmentsChangeTheRemoteRows()
    {
        var targets = Targets();
        var model = new CodeLandingModel(targets);
        model.SetRemote(RemoteLoadState.Loaded);

        targets.SetProfiles([Ssh("box-9")]);

        model.Cards[3].Rows.Select(static row => row.OptionId).ShouldBe(["ssh:box-9"]);
    }

    /// <summary>A card button raises the request with its action and environment, for the window to act on.</summary>
    [Fact]
    public void ARequestRaisesTheActionAndEnvironment()
    {
        var model = new CodeLandingModel(Targets(Ssh("box-1")));
        CodeLandingActionEventArgs? seen = null;
        model.ActionRequested += (_, e) => seen = e;

        model.Request(CodeLandingAction.StartOnEnvironment, "ssh:box-1");

        seen.ShouldNotBeNull();
        seen.Action.ShouldBe(CodeLandingAction.StartOnEnvironment);
        seen.OptionId.ShouldBe("ssh:box-1");
    }

    /// <summary>The status of omp cannot be null: a null status is refused before it reaches a card.</summary>
    [Fact]
    public void ANullOmpStatusIsRefused()
    {
        var model = new CodeLandingModel(Targets());

        Should.Throw<ArgumentNullException>(() => model.SetLocal(null!));
    }

    /// <summary>The account and machine states the linked-machine card can be in.</summary>
    [SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Test data enum for a public [Theory] method in a public xUnit test class; xUnit1000 requires the class to stay public, so the enum it exposes must be public too.")]
    public enum LinkedCase
    {
        /// <summary>Signed out.</summary>
        SignedOut,

        /// <summary>No account host.</summary>
        NoAccount,

        /// <summary>Signed in, but the API has no sign-in configured.</summary>
        NotConfigured,

        /// <summary>Signed in, but the service cannot be reached.</summary>
        Offline,

        /// <summary>Signed in with no source of linked machines in this window.</summary>
        NoSource,

        /// <summary>Signed in with no linked machines.</summary>
        NoMachines,

        /// <summary>Signed in with linked machines.</summary>
        Machines,
    }

    private static CodeLandingInputs LinkedInputs(LinkedCase linkedCase) => linkedCase switch
    {
        LinkedCase.SignedOut => Inputs(account: Account(AuthPhase.SignedOut)),
        LinkedCase.NoAccount => Inputs(account: null),
        LinkedCase.NotConfigured => Inputs(account: Account(AuthPhase.SignedIn, configured: false)),
        LinkedCase.Offline => Inputs(account: SignedIn(offline: true), linked: ["laptop"]),
        LinkedCase.NoSource => Inputs(account: SignedIn(), linked: null),
        LinkedCase.NoMachines => Inputs(account: SignedIn(), linked: []),
        _ => Inputs(account: SignedIn(), linked: ["laptop"]),
    };

    private static CodeLandingInputs Inputs(
        LocalOmpStatus? local = null,
        RemoteLoadState remote = RemoteLoadState.Loaded,
        AccountHostState? account = null,
        IReadOnlyList<string>? linked = null) =>
        new(local ?? LocalOmpStatus.Installed(Version, LocalOmpSource.Path), remote, account, linked);

    private static AccountHostState? Account(AuthPhase phase, bool configured = true) =>
        new(phase, "a@example.com", false, false, false, false, false, configured, null, null, 0, false, null, false);

    private static AccountHostState SignedIn(bool offline = false) =>
        new(AuthPhase.SignedIn, "a@example.com", offline, false, false, false, false, true, null, null, 0, false, null, false);

    private static NewSessionTargetModel Targets(params EnvironmentProfile[] profiles) => new(profiles);

    private static IReadOnlyList<CodeLandingCard> Cards(CodeLandingInputs inputs, NewSessionTargetModel? targets = null) =>
        CodeLandingModel.Build(inputs, (targets ?? Targets()).Options);

    private static CodeLandingCard Card(IReadOnlyList<CodeLandingCard> cards, CodeLandingCardKind kind) => cards.Single(card => card.Kind == kind);

    private static EnvironmentProfile Ssh(string id) => new()
    {
        Id = id,
        DisplayName = "Box " + id,
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = "alias-" + id, RemoteWorkspaceRoot = "/srv/app" },
    };

    private static EnvironmentProfile Container(string id) => new()
    {
        Id = id,
        DisplayName = "Dev " + id,
        Kind = EnvironmentKind.Container,
        Container = new ContainerProfile { Container = "devbox", WorkspaceFolder = "/workspaces/app" },
    };
}
