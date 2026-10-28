namespace OmpOS.Web.Services;

/// <summary>What a native slash command can reach in the web head. A command whose target is null is listed as unavailable and does not run.</summary>
internal sealed record WebSlashTargets
{
    /// <summary>Gets the target of <c>/settings</c>: opens the Settings dialog on the named page. It returns false when there is no such page.</summary>
    public Func<string, bool>? OpenSettings { get; init; }

    /// <summary>Gets the target of a new chat.</summary>
    public Func<CancellationToken, Task>? NewChat { get; init; }

    /// <summary>Gets the target of the mode commands: sets the mode of the session and returns a failure sentence, or null on success.</summary>
    public Func<string, CancellationToken, Task<string?>>? SetMode { get; init; }

    /// <summary>Gets the target of the model command: filters the model list and returns the sentence to show.</summary>
    public Func<string, CancellationToken, Task<string>>? PickModel { get; init; }

    /// <summary>Gets the target of the help command: puts the typed text in the composer.</summary>
    public Action<string>? ShowHelp { get; init; }
}
