namespace OmpOS.Native.Views.Sessions;

/// <summary>
/// The pieces of the desktop head that native slash actions drive. The head fills in what only it can reach (Settings, the agent rail, the terminal panel, the side chat panel); the session runtime
/// fills in what its own composer reaches (the mode chip, the model picker, the slash palette). A target left null makes its action unavailable: the palette then shows the command disabled instead of listing a control that does nothing.
/// </summary>
public sealed record DesktopSlashTargets
{
    /// <summary>Gets the callback that opens Settings. Its argument is a section (<c>models</c>, <c>agents</c>, <c>plugins</c>, <c>rules</c>, <c>skills</c> or <c>commands</c>) or an empty string for the first page. It returns <see langword="false"/> for a section the window does not have.</summary>
    public Func<string, bool>? OpenSettings { get; init; }

    /// <summary>Gets the callback that starts a new chat.</summary>
    public Func<CancellationToken, Task>? NewChat { get; init; }

    /// <summary>Gets the callback that starts a new terminal session.</summary>
    public Func<CancellationToken, Task>? NewTerminal { get; init; }

    /// <summary>Gets the callback that switches the session mode. Its argument is the mode name; it returns <see langword="null"/> when the mode changed, or the reason it did not.</summary>
    public Func<string, CancellationToken, Task<string?>>? SetMode { get; init; }

    /// <summary>Gets the callback that filters the model picker and selects the model when exactly one matches. It returns the sentence to show.</summary>
    public Func<string, CancellationToken, Task<string>>? PickModel { get; init; }

    /// <summary>Gets the callback that opens a side chat. Its argument is the first question, or an empty string.</summary>
    public Func<string, CancellationToken, Task>? OpenSideChat { get; init; }

    /// <summary>Gets the callback that opens the slash palette filtered by the typed text.</summary>
    public Action<string>? ShowHelp { get; init; }
}
