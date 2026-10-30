using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Fakes;

using Shouldly;

using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>
/// Composition root. 73-03 appends Tools, Frames and Audit composition only through a separate ComputerUseToolsComposition file.
/// </summary>
public sealed class ComputerUseCompositionTests
{
    [Fact]
    public async Task InvalidOptionsFailNamingKeysAndNeverValues()
    {
        const string secret = "not-a-rate";
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => ComputerUseComposition.CreateAsync(Options(static variable =>
                string.Equals(variable, ComputerUseOptionsReader.VariableName(nameof(ComputerUseOptions.RatePerMinute)), StringComparison.Ordinal) ? secret : "true")).AsTask()).ConfigureAwait(true);
        exception.Message.ShouldContain(ComputerUseOptionsValidator.Key(nameof(ComputerUseOptions.RatePerMinute)));
        exception.Message.ShouldNotContain(secret);
    }

    [Fact]
    public async Task DisabledBuildsAGateThatDeniesDisabled()
    {
        ComputerUseCore core = await ComputerUseComposition.CreateAsync(Options(static _ => null), TestContext.Current.CancellationToken).ConfigureAwait(true);
        core.Options.Enabled.ShouldBeFalse();
        ComputerGateDecision decision = await core.Gate.AuthorizeAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        decision.ShouldBeOfType<ComputerGateDenied>().Code.ShouldBe(DenialCode.Disabled);
    }

    [Fact]
    public async Task SignedOutOrNonCoworkDeniesAtStep1()
    {
        ComputerUseCore signedOut = await ComputerUseComposition.CreateAsync(Options(
            static name => string.Equals(name, ComputerUseOptionsReader.VariableName(nameof(ComputerUseOptions.Enabled)), StringComparison.Ordinal) ? "true" : null,
            signedIn: false), TestContext.Current.CancellationToken).ConfigureAwait(true);
        signedOut.ActionGate.OptIn("session");
        ComputerGateDecision signedOutDecision = await signedOut.Gate.AuthorizeAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        ComputerGateDenied signedOutDenial = signedOutDecision.ShouldBeOfType<ComputerGateDenied>();
        signedOutDenial.Code.ShouldBe(DenialCode.Disabled);

        ComputerUseCore other = await ComputerUseComposition.CreateAsync(Options(
            static name => string.Equals(name, ComputerUseOptionsReader.VariableName(nameof(ComputerUseOptions.Enabled)), StringComparison.Ordinal) ? "true" : null,
            cowork: false), TestContext.Current.CancellationToken).ConfigureAwait(true);
        other.ActionGate.OptIn("session");
        ComputerGateDecision otherDecision = await other.Gate.AuthorizeAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        ComputerGateDenied otherDenial = otherDecision.ShouldBeOfType<ComputerGateDenied>();
        otherDenial.Code.ShouldBe(DenialCode.Disabled);
    }

    private static ComputerUseCompositionOptions Options(Func<string, string?> getVariable, bool signedIn = true, bool cowork = true) => new()
    {
        GetVariable = getVariable,
        Settings = new MemorySettings(),
        Backend = new FakeComputerUseBackend(),
        Clipboard = new Clipboard(),
        Prompt = new AllowPrompt(),
        Hotkey = new Hotkey(),
        Access = new Access(signedIn, cowork),
        Time = TimeProvider.System,
        StateDirectory = Path.GetTempPath(),
    };

    private static ComputerActionRequest Request() => new()
    {
        SessionId = "session",
        ToolName = "computer_click",
        Origin = ComputerActionOrigin.Local,
        ActionHash = new byte[] { 1 },
    };

    private sealed class Access(bool signedIn, bool cowork) : IComputerUseAccessFacts
    {
        public bool IsSignedIn { get; } = signedIn;

        public bool IsCoworkSession(string sessionId) => cowork;
    }

    private sealed class Clipboard : IClipboardTextPort
    {
        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);

        public ValueTask WriteAsync(string text, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class AllowPrompt : IComputerApprovalPrompt
    {
        public ValueTask<ComputerApprovalChoice> RequestAsync(ComputerApprovalFacts facts, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ComputerApprovalChoice.Deny);
    }

    private sealed class Hotkey : IGlobalHotkeyRegistrar
    {
        public ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken) => ValueTask.FromResult(true);

        public ValueTask UnregisterAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
