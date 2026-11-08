using Autonogrammer.Contracts.Environments;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views.Landing;
using Autonogrammer.Presentation.Landing;
using Autonogrammer.Presentation.Remote;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Landing;

/// <summary>The Code landing view alone: its four cards in order, the names and tab order a keyboard user gets, and a press that only asks the model.</summary>
public sealed class CodeLandingViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The four cards are drawn in the fixed order, and each frame carries its accessible name.</summary>
    [Fact]
    public async Task TheFourCardsAreDrawnInTheFixedOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, _, window) = Open(Ssh("box-1"));

            var titles = view.GetVisualDescendants()
                .OfType<Border>()
                .Where(static border => border.Classes.Contains("card"))
                .Select(static border => AutomationProperties.GetName(border)!.Split(". ")[0])
                .ToList();
            titles.ShouldBe([CodeLandingCopy.LocalTitle, CodeLandingCopy.LinkedTitle, CodeLandingCopy.CloudTitle, CodeLandingCopy.RemoteTitle]);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The keyboard reaches the enabled controls in card order, every one of them is named, and the disabled cards are skipped.</summary>
    [Fact]
    public async Task TheKeyboardVisitsTheEnabledCardsInOrderAndEveryStopIsNamed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, window) = Open(Ssh("box-1"));
            var remote = model.Cards[3];

            ViewHarness.TabOrder(view).ShouldBe(
            [
                model.Cards[0].Primary!.AccessibleName,
                remote.Rows[0].AccessibleName,
                remote.Actions[0].AccessibleName,
            ]);
            ViewHarness.Unnamed(view).ShouldBeEmpty();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Each card's own button carries the card's accessible name, which says its title, state and Omp status.</summary>
    [Fact]
    public async Task ACardButtonCarriesTheCardsAccessibleName()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, window) = Open();

            var primary = view.GetVisualDescendants().OfType<Button>().First();
            AutomationProperties.GetName(primary).ShouldBe(model.Cards[0].AccessibleName);
            AutomationProperties.GetName(primary).ShouldBe("Local machine. Ready on this computer. Omp 1.2.3 from PATH.");
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Pressing a card's button raises the request for its action and environment; the view starts nothing itself.</summary>
    [Fact]
    public async Task PressingACardRaisesItsRequest()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, window) = Open(Ssh("box-1"));
            var requests = new List<(CodeLandingAction Action, string? OptionId)>();
            model.ActionRequested += (_, e) => requests.Add((e.Action, e.OptionId));

            ViewHarness.Click(window, view.GetVisualDescendants().OfType<Button>().First());

            requests.ShouldBe([(CodeLandingAction.StartOnEnvironment, NewSessionTargetModel.LocalId)]);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A change to the model redraws the cards, so the text a user reads is the text the model holds.</summary>
    [Fact]
    public async Task AModelChangeRedrawsTheText()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, window) = Open();

            model.SetLocal(LocalOmpStatus.NotInstalled("Install omp."));
            ViewHarness.Drain();

            ViewHarness.VisibleTexts(view).ShouldContain("Omp is not installed. Install omp.", StringComparer.Ordinal);
            ViewHarness.VisibleTexts(view).ShouldNotContain("Omp 1.2.3 from PATH.", StringComparer.Ordinal);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    private static (CodeLandingView View, CodeLandingModel Model, Window Window) Open(params EnvironmentProfile[] profiles)
    {
        var targets = new NewSessionTargetModel(profiles);
        var model = new CodeLandingModel(targets);
        model.SetLocal(LocalOmpStatus.Installed("1.2.3", LocalOmpSource.Path));
        model.SetRemote(RemoteLoadState.Loaded);
        model.SetAccount(null);
        var view = new CodeLandingView { DataContext = model };
        var window = ViewHarness.Show(view);
        return (view, model, window);
    }

    private static EnvironmentProfile Ssh(string id) => new()
    {
        Id = id,
        DisplayName = "Box " + id,
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = "alias-" + id, RemoteWorkspaceRoot = "/srv/app" },
    };
}
