using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Cowork;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Accounts;
using Autonogrammer.Presentation.Cowork;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>
/// The signed-out Cowork panel. A configured API shows the sign-in button, and activating it hands the sign-in seam a model over the head's account
/// host. An unconfigured API shows the not-configured sentence and no button. Neither opens a Cowork folder.
/// </summary>
public sealed class CoworkSignInButtonTests
{
    private const string DockBodyName = "DockPanelBody";
    private const string SignInButtonName = "CoworkSignInButton";
    private const string ApiAddress = "https://api.example.test/";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>With an API address the panel shows the sentence and a named button. Activating the button calls the seam once with a model over the head's host.</summary>
    [Fact]
    public async Task AConfiguredSignedOutHeadShowsTheSignInButtonAndHandsTheSeamAModelOnTheHeadsAccountHost()
    {
        using var tree = new TempTree();
        var services = await HeadServices.CreateAsync(HeadOptions(tree, ConfiguredVariables), Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, null, services: services)).ConfigureAwait(true);
        await using var headScope = head.ConfigureAwait(true);
        services.AccountHost.State.Configured.ShouldBeTrue("the head has an API address, so its account host is configured");
        new NativeCoworkAccountGate(services.AccountHost.Session).IsSignedIn.ShouldBeFalse("the fixture head has no stored account");

        var opened = new List<AccountViewModel>();
        var cowork = CoworkSurfaceServicesFactory.TryBuild(head, opened.Add);

        cowork.ShouldNotBeNull("a signed-out head with Cowork services still shows the switch");
        Directory.Exists(Path.Combine(tree.State, CoworkAccountFolder.RootName)).ShouldBeFalse("signed out, no registry or state file is opened or created");

        var session = TestAppBuilder.StartSession();
        await using var sessionScope = session.ConfigureAwait(true);
        await session.RunAsync(() =>
        {
            var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
            var window = new MainWindow(panel, MotionPreference.System, cowork: cowork) { Width = 1280, Height = 800 };
            window.Show();
            ShellHarness.Settle(window);
            try
            {
                var host = window.Content.ShouldBeOfType<SurfaceHost>();
                host.Switch.Select(SessionSurface.Cowork);
                ShellHarness.Settle(window);

                Bodies(host).ShouldBe([CoworkCopy.SignInToUseCowork]);
                var button = SignInButtons(host).ShouldHaveSingleItem();
                button.Content.ShouldBe(CoworkCopy.SignInButtonLabel);
                AutomationProperties.GetName(button).ShouldBe(CoworkCopy.SignInButtonLabel, "the accessible name is the visible label");
                button.IsTabStop.ShouldBeTrue("the button is reachable by keyboard");
                opened.ShouldBeEmpty("nothing opens until the button is activated");

                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ShellHarness.Settle(window);

                opened.Count.ShouldBe(1, "one activation calls the seam once");
                opened[0].ShowSignedOut.ShouldBeTrue("the model opens in the signed-out phase");
                opened[0].Configured.ShouldBeTrue("the model reads the head's configured account host");
                Directory.Exists(Path.Combine(tree.State, CoworkAccountFolder.RootName)).ShouldBeFalse("signing in is the only path that opens the registry");
            }
            finally
            {
                window.Close();
                panel.Dispose();
            }

            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Without an API address the panel shows the not-configured sentence and no button, and the seam is never called.</summary>
    [Fact]
    public async Task ANotConfiguredSignedOutHeadShowsTheNotConfiguredSentenceAndNoButton()
    {
        using var tree = new TempTree();
        var services = await HeadServices.CreateAsync(HeadOptions(tree, static _ => null), Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, null, services: services)).ConfigureAwait(true);
        await using var headScope = head.ConfigureAwait(true);
        services.AccountHost.State.Configured.ShouldBeFalse("no API address is set");

        var opened = new List<AccountViewModel>();
        var cowork = CoworkSurfaceServicesFactory.TryBuild(head, opened.Add);

        cowork.ShouldNotBeNull();
        Directory.Exists(Path.Combine(tree.State, CoworkAccountFolder.RootName)).ShouldBeFalse("signed out, no registry or state file is opened or created");

        var session = TestAppBuilder.StartSession();
        await using var sessionScope = session.ConfigureAwait(true);
        await session.RunAsync(() =>
        {
            var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
            var window = new MainWindow(panel, MotionPreference.System, cowork: cowork) { Width = 1280, Height = 800 };
            window.Show();
            ShellHarness.Settle(window);
            try
            {
                var host = window.Content.ShouldBeOfType<SurfaceHost>();
                host.Switch.Select(SessionSurface.Cowork);
                ShellHarness.Settle(window);

                Bodies(host).ShouldBe([AccountCopy.NotConfigured]);
                SignInButtons(host).ShouldBeEmpty("an unconfigured API has nothing to sign in to");
                opened.ShouldBeEmpty();
                Directory.Exists(Path.Combine(tree.State, CoworkAccountFolder.RootName)).ShouldBeFalse();
            }
            finally
            {
                window.Close();
                panel.Dispose();
            }

            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The content carries the state, the sentence, the label and the request. The not-configured content has no label and its request does nothing.</summary>
    [Fact]
    public void TheSignedOutContentCarriesTheSentenceTheLabelAndTheRequest()
    {
        var requests = 0;
        var available = new CoworkSignedOutContent(CoworkSignedOutState.SignInAvailable, () => requests++);
        available.State.ShouldBe(CoworkSignedOutState.SignInAvailable);
        available.Sentence.ShouldBe(CoworkCopy.SignInToUseCowork);
        available.ButtonLabel.ShouldBe(CoworkCopy.SignInButtonLabel);
        available.RequestSignIn();
        requests.ShouldBe(1);

        var notConfigured = new CoworkSignedOutContent(CoworkSignedOutState.NotConfigured, signIn: null);
        notConfigured.State.ShouldBe(CoworkSignedOutState.NotConfigured);
        notConfigured.Sentence.ShouldBe(AccountCopy.NotConfigured);
        notConfigured.ButtonLabel.ShouldBeNull();
        notConfigured.RequestSignIn();

        Should.Throw<ArgumentException>(() =>
        {
            _ = new CoworkSignedOutContent(CoworkSignedOutState.SignInAvailable, signIn: null);
        });
    }

    private static HeadServicesOptions HeadOptions(TempTree tree, Func<string, string?> variables) => new()
    {
        StateRoot = tree.State,
        PluginsRoot = tree.Plugins,
        WorkspaceRoot = tree.Workspace,
        HomeDirectory = Path.Combine(tree.Root, "home"),
        ConfigDirectory = Path.Combine(tree.Root, "config"),
        GetVariable = variables,
        ApiHandler = new NoRequestHandler(),
    };

    private static string? ConfiguredVariables(string name) => string.Equals(name, HeadServices.ApiBaseUrlVariable, StringComparison.Ordinal) ? ApiAddress : null;

    private static List<string?> Bodies(SurfaceHost host) =>
        host.Cowork.ShouldNotBeNull().GetVisualDescendants().OfType<TextBlock>()
            .Where(block => string.Equals(block.Name, DockBodyName, StringComparison.Ordinal))
            .Select(block => block.Text)
            .ToList();

    private static List<Button> SignInButtons(SurfaceHost host) =>
        host.Cowork.ShouldNotBeNull().GetVisualDescendants().OfType<Button>()
            .Where(button => string.Equals(button.Name, SignInButtonName, StringComparison.Ordinal))
            .ToList();

    /// <summary>Fails the test if the head reaches the API. The signed-out panel must not need one.</summary>
    private sealed class NoRequestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Opening the signed-out head made an API request.");
    }
}
