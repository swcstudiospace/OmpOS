namespace OmpOS.Presentation.Commands;

/// <summary>
/// The data that describes one slash command: its name, arguments, availability, and the handler the host dispatches it to.
/// The descriptor holds data only; it runs nothing.
/// </summary>
public sealed record SlashCommandDescriptor
{
    /// <summary>
    /// Gets the command name without the leading slash. The name is one that the slash-command recognizer accepts, so a
    /// user can type it. The registry rejects any other name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Gets the arguments in the order they are typed.</summary>
    public IReadOnlyList<SlashCommandArgument> Arguments { get; init; } = [];

    /// <summary>Gets the text that tells the user what the command does.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the platforms on which the command is available. The registry does not filter by platform; the host applies this
    /// value. The registry refuses a value that holds a bit <see cref="CommandPlatforms"/> does not define.
    /// </summary>
    public required CommandPlatforms Platforms { get; init; }

    /// <summary>Gets the reason the command is disabled, or <see langword="null"/> when it is enabled. The reason is returned unchanged.</summary>
    public string? DisabledReason { get; init; }

    /// <summary>
    /// Gets the identifier of the handler that runs the command. It is an identifier and not a delegate, so the descriptor
    /// stays plain data; a host maps the identifier to its handler. For <see cref="SlashHandlerKind.NativeAction"/> it is one of
    /// <see cref="SlashActionIds"/>; for <see cref="SlashHandlerKind.OmpDelegated"/> it is <c>omp.</c> and the omp command that runs it;
    /// for <see cref="SlashHandlerKind.Skill"/> it is <c>skill.</c> and the skill name.
    /// </summary>
    public required string HandlerId { get; init; }

    /// <summary>
    /// Gets what runs the command. A descriptor that names no kind is sent to the engine as typed, which is how omp's own commands work.
    /// </summary>
    public SlashHandlerKind Kind { get; init; } = SlashHandlerKind.OmpDelegated;

    /// <summary>Gets how completely OmpOS honours the Cursor command. <see cref="SlashCommandStatus.Unavailable"/> requires <see cref="DisabledReason"/>.</summary>
    public SlashCommandStatus Status { get; init; } = SlashCommandStatus.Delegated;

    /// <summary>Gets where Cursor documents the command.</summary>
    public SlashCommandGroup Group { get; init; } = SlashCommandGroup.Chat;

    /// <summary>Gets the fixed argument a native action receives (a settings section, a mode, a model filter), or <see langword="null"/>.</summary>
    public string? ActionArgument { get; init; }

    /// <summary>
    /// Gets the omp command that is sent in place of this token when it differs from the token (<c>summarize</c> is sent as <c>compact</c>), or
    /// <see langword="null"/> when the token is sent as typed.
    /// </summary>
    public string? EngineCommand { get; init; }

    /// <summary>Gets the token this one is an alias of in Cursor's documentation, or <see langword="null"/>.</summary>
    public string? AliasOf { get; init; }

    /// <summary>Gets one sentence that says how OmpOS runs the command, or what is missing when it runs only in part; <see langword="null"/> for an unavailable command, whose reason is <see cref="DisabledReason"/>.</summary>
    public string? Note { get; init; }
}
