using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands;

/// <summary>The dispatcher carries out the resolver's decision: it runs a native action through the host, or tells the composer what to send.</summary>
public sealed class SlashCommandDispatcherTests
{
    private static readonly SlashCommandDispatcher Dispatcher = new(new SlashCommandResolver(SlashCommandCatalogue.Shipped));

    /// <summary>Text no source knows is sent as typed.</summary>
    [Fact]
    public async Task UnknownTextIsSentAsTyped()
    {
        var result = await Dispatcher.DispatchAsync("hello /plan", SlashResolutionContext.Empty, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Kind.ShouldBe(SlashDispatchKind.SendPrompt);
        result.Text.ShouldBe("hello /plan");
    }

    /// <summary>A delegated command is sent to omp as the prompt.</summary>
    [Fact]
    public async Task ADelegatedCommandIsSent()
    {
        var result = await Dispatcher.DispatchAsync("/summarize", SlashResolutionContext.Empty, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Kind.ShouldBe(SlashDispatchKind.SendPrompt);
        result.Text.ShouldBe("/compact");
    }

    /// <summary>A refused command sends nothing and shows the reason.</summary>
    [Fact]
    public async Task ARefusedCommandSendsNothing()
    {
        var result = await Dispatcher.DispatchAsync("/in-cloud fix the bug", SlashResolutionContext.Empty, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Kind.ShouldBe(SlashDispatchKind.Handled);
        result.Text.ShouldBe("cloud agents: phase 60");
    }

    /// <summary>A completed native action shows its message and sends nothing; the host sees the action, the catalogue's argument and the typed text.</summary>
    [Fact]
    public async Task ACompletedNativeActionIsHandled()
    {
        var host = new RecordingHost(SlashActionResult.Completed("Settings opened."));

        var result = await Dispatcher.DispatchAsync("/rules", new SlashResolutionContext([], SlashDiscovery.Empty, host), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Kind.ShouldBe(SlashDispatchKind.Handled);
        result.Text.ShouldBe("Settings opened.");
        host.Requests.ShouldBe([new SlashActionRequest(SlashActionIds.SettingsOpen, "rules", string.Empty)]);
    }

    /// <summary>An action that completes with a follow-up prompt (the text after /plan) sends that prompt.</summary>
    [Fact]
    public async Task AFollowUpPromptIsSent()
    {
        var host = new RecordingHost(SlashActionResult.Completed("Mode: plan.", "add a cache"));

        var result = await Dispatcher.DispatchAsync("/plan add a cache", new SlashResolutionContext([], SlashDiscovery.Empty, host), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Kind.ShouldBe(SlashDispatchKind.SendPrompt);
        result.Text.ShouldBe("add a cache");
    }

    /// <summary>A failed action shows the reason and sends nothing, even when it carried text to send afterwards.</summary>
    [Fact]
    public async Task AFailedActionIsHandledWithItsReason()
    {
        var host = new RecordingHost(SlashActionResult.Failed("The engine offers no ask mode."));

        var result = await Dispatcher.DispatchAsync("/ask why", new SlashResolutionContext([], SlashDiscovery.Empty, host), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Kind.ShouldBe(SlashDispatchKind.Handled);
        result.Text.ShouldBe("The engine offers no ask mode.");
    }

    /// <summary>Null input is refused.</summary>
    [Fact]
    public async Task NullArgumentsAreRefused()
    {
        var token = TestContext.Current.CancellationToken;
        await Should.ThrowAsync<ArgumentNullException>(() => Dispatcher.DispatchAsync(null!, SlashResolutionContext.Empty, token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentNullException>(() => Dispatcher.DispatchAsync("/x", null!, token)).ConfigureAwait(true);
        Should.Throw<ArgumentNullException>(() => new SlashCommandDispatcher(null!));
    }

    private sealed class RecordingHost(SlashActionResult result) : ISlashActionHost
    {
        public List<SlashActionRequest> Requests { get; } = [];

        public bool CanRun(string actionId) => true;

        public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }
}
