using Autonogrammer.Presentation.Accounts;
using Autonogrammer.UI.Components.Account;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Account;

/// <summary>
/// The sign-in options the panel offers beside the email code: only the providers the deployment enables, the wallet choice only when a wallet is
/// present, and the sentence that follows each choice.
/// </summary>
public sealed class SignInOptionsTests : UiTestContext
{
    [Fact]
    public void OnlyTheEnabledProvidersAreOffered()
    {
        var host = new OptionsHost(["google"], wallet: false);

        var cut = Render<SignInPanel>(parameters => parameters.Add(p => p.Host, host));

        cut.WaitForAssertion(() => ButtonLabels(cut).ShouldBe(["Continue with Google"]));
    }

    [Fact]
    public void TheWalletChoiceAppearsOnlyWhenAWalletIsPresent()
    {
        var host = new OptionsHost([], wallet: true);

        var cut = Render<SignInPanel>(parameters => parameters.Add(p => p.Host, host));

        cut.WaitForAssertion(() => ButtonLabels(cut).ShouldBe([AccountCopy.WalletButtonLabel]));
    }

    [Fact]
    public void StartingAProviderSaysItsTabOpened()
    {
        var host = new OptionsHost(["google"], wallet: false, startResult: true);
        var cut = Render<SignInPanel>(parameters => parameters.Add(p => p.Host, host));
        cut.WaitForAssertion(() => ButtonLabels(cut).ShouldBe(["Continue with Google"]));

        Click(cut, "Continue with Google");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain(AccountCopy.ProviderStartedSentence));
        host.StartedProviders.ShouldBe(["google"]);
    }

    [Fact]
    public void AProviderThatCannotStartSaysSo()
    {
        var host = new OptionsHost(["google"], wallet: false, startResult: false);
        var cut = Render<SignInPanel>(parameters => parameters.Add(p => p.Host, host));
        cut.WaitForAssertion(() => ButtonLabels(cut).ShouldBe(["Continue with Google"]));

        Click(cut, "Continue with Google");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain(AccountCopy.ProviderUnavailableSentence));
    }

    [Theory]
    [InlineData(WalletSignInOutcome.Declined, AccountCopy.WalletDeclinedSentence)]
    [InlineData(WalletSignInOutcome.Unavailable, AccountCopy.WalletUnavailableSentence)]
    [InlineData(WalletSignInOutcome.Failed, AccountCopy.WalletFailedSentence)]
    public void AWalletOutcomeSaysWhatHappened(WalletSignInOutcome outcome, string sentence)
    {
        var host = new OptionsHost([], wallet: true, walletOutcome: outcome);
        var cut = Render<SignInPanel>(parameters => parameters.Add(p => p.Host, host));
        cut.WaitForAssertion(() => ButtonLabels(cut).ShouldBe([AccountCopy.WalletButtonLabel]));

        Click(cut, AccountCopy.WalletButtonLabel);

        cut.WaitForAssertion(() => cut.Markup.ShouldContain(sentence));
    }

    // Only the group of other ways counts: the panel's own form has a "Send code" button of its own.
    private static string[] ButtonLabels(IRenderedComponent<SignInPanel> cut) =>
        cut.FindAll(".autonogrammer-connect-alternatives button").Select(static button => button.TextContent.Trim()).ToArray();

    private static void Click(IRenderedComponent<SignInPanel> cut, string label) =>
        cut.FindAll(".autonogrammer-connect-alternatives button").Single(button => string.Equals(button.TextContent.Trim(), label, StringComparison.Ordinal)).Click();

    /// <summary>A host that offers the given providers and wallet, and answers each choice with the configured outcome.</summary>
    private sealed class OptionsHost(IReadOnlyList<string> providers, bool wallet, bool startResult = true, WalletSignInOutcome walletOutcome = WalletSignInOutcome.Declined)
        : IAccountHost, IProviderSignInHost, IWalletSignInHost
    {
        private readonly List<string> _started = [];

        public IReadOnlyList<string> StartedProviders => _started;

        public event EventHandler? Changed { add { } remove { } }

        public AccountHostState State { get; } = AccountHostState.SignedOut;

        public Task SendCodeAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResendCodeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SubmitCodeAsync(string code, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SignOutAsync(bool everywhere, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ChangeEmail()
        {
        }

        public Task RefreshDevicesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetEnabledProvidersAsync(CancellationToken cancellationToken) => Task.FromResult(providers);

        public Task<bool> StartProviderSignInAsync(string provider, CancellationToken cancellationToken)
        {
            _started.Add(provider);
            return Task.FromResult(startResult);
        }

        public Task<bool> WalletAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(wallet);

        public Task<WalletSignInOutcome> SignInWithWalletAsync(CancellationToken cancellationToken) => Task.FromResult(walletOutcome);
    }
}
