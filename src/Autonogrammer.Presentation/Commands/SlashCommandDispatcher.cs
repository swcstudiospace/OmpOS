namespace Autonogrammer.Presentation.Commands;

/// <summary>Carries out what <see cref="SlashCommandResolver"/> decided: runs a native action through the head, or tells the composer which prompt to send.</summary>
public sealed class SlashCommandDispatcher
{
    private readonly SlashCommandResolver _resolver;

    /// <summary>Initializes a new instance of the <see cref="SlashCommandDispatcher"/> class.</summary>
    /// <param name="resolver">The resolver.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is null.</exception>
    public SlashCommandDispatcher(SlashCommandResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    /// <summary>Resolves and carries out typed composer text.</summary>
    /// <param name="input">The text as typed.</param>
    /// <param name="context">The engine commands, the discovered conventions and the action host.</param>
    /// <param name="cancellationToken">Cancels a native action.</param>
    /// <returns>Send a prompt (the text as typed, or rewritten) or show a sentence and send nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="context"/> is null.</exception>
    public async Task<SlashDispatch> DispatchAsync(string input, SlashResolutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var resolution = _resolver.Resolve(input, context);
        switch (resolution)
        {
            case SlashPassThrough passThrough:
                return new SlashDispatch(SlashDispatchKind.SendPrompt, passThrough.Text, resolution);
            case SlashSendPrompt send:
                return new SlashDispatch(SlashDispatchKind.SendPrompt, send.Prompt, resolution);
            case SlashRefused refused:
                return new SlashDispatch(SlashDispatchKind.Handled, refused.Reason, resolution);
            case SlashRunAction run when context.Host is { } host:
                return await RunAsync(run, host, cancellationToken).ConfigureAwait(false);
            default:
                return new SlashDispatch(SlashDispatchKind.Handled, SlashPaletteCopy.ActionNotInThisWindow, resolution);
        }
    }

    private static async Task<SlashDispatch> RunAsync(SlashRunAction run, ISlashActionHost host, CancellationToken cancellationToken)
    {
        var result = await host.RunAsync(run.Request, cancellationToken).ConfigureAwait(false);
        return result.Outcome == SlashActionOutcome.Completed && !string.IsNullOrWhiteSpace(result.FollowUpPrompt)
            ? new SlashDispatch(SlashDispatchKind.SendPrompt, result.FollowUpPrompt, run)
            : new SlashDispatch(SlashDispatchKind.Handled, result.Message, run);
    }
}
