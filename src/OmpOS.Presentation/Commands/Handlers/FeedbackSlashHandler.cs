namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>
/// Runs <c>/feedback [text]</c> (<see cref="HandlerActionIds.AppFeedback"/>): it opens the issue form of the OmpOS repository in the browser through <see cref="IUrlLauncher"/>. The text typed after the command becomes the issue title
/// (cut at 200 characters). Nothing is sent from the application: the user writes and submits the issue in the browser. Only an <c>https</c> address is ever opened.
/// </summary>
public sealed class FeedbackSlashHandler : ISlashActionHandler
{
    /// <summary>The address of the issue form of the OmpOS repository.</summary>
    public const string DefaultIssuesUrl = "https://github.com/swcstudiospace/OmpOS/issues/new";

    private const int MaxTitleChars = 200;

    private readonly IUrlLauncher _launcher;
    private readonly string _issuesAddress;

    /// <summary>Creates the handler.</summary>
    /// <param name="launcher">Opens an address in the browser.</param>
    /// <param name="issuesAddress">The address of the issue form; <see cref="DefaultIssuesUrl"/> when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="launcher"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="issuesAddress"/> is not an absolute https address.</exception>
    public FeedbackSlashHandler(IUrlLauncher launcher, string? issuesAddress = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        var url = issuesAddress ?? DefaultIssuesUrl;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new ArgumentException("The feedback address must be an absolute https address.", nameof(issuesAddress));
        }

        _launcher = launcher;
        _issuesAddress = parsed.AbsoluteUri;
    }

    /// <inheritdoc/>
    public string ActionId => HandlerActionIds.AppFeedback;

    /// <inheritdoc/>
    public bool CanRun => true;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = request.TypedText.ReplaceLineEndings(" ").Trim();
        if (title.Length > MaxTitleChars)
        {
            title = title[..MaxTitleChars];
        }

        var url = title.Length == 0 ? _issuesAddress : _issuesAddress + "?title=" + Uri.EscapeDataString(title);
        return await _launcher.OpenAsync(new Uri(url), cancellationToken).ConfigureAwait(true)
            ? SlashActionResult.Completed(HandlerCopy.FeedbackOpened())
            : SlashActionResult.Failed(HandlerCopy.FeedbackNotOpened(_issuesAddress));
    }
}
