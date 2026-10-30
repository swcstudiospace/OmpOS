namespace Autonogrammer.Workspace.Tests;

/// <summary>
/// A prompt that answers the same way every time and records every request it receives.
/// </summary>
internal sealed class ScriptedSetupConsentPrompt : ISetupConsentPrompt
{
    private readonly bool _grant;
    private readonly List<SetupConsentRequest> _requests = [];

    private ScriptedSetupConsentPrompt(bool grant) => _grant = grant;

    /// <summary>Gets the requests received, oldest first.</summary>
    public IReadOnlyList<SetupConsentRequest> Requests => _requests;

    /// <summary>Creates a prompt that approves every request.</summary>
    public static ScriptedSetupConsentPrompt Approving() => new(grant: true);

    /// <summary>Creates a prompt that refuses every request.</summary>
    public static ScriptedSetupConsentPrompt Refusing() => new(grant: false);

    /// <inheritdoc />
    public Task<SetupConsentDecision> AskAsync(SetupConsentRequest request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(_grant ? SetupConsentDecision.Granted() : SetupConsentDecision.Denied("the user pressed Deny"));
    }
}
