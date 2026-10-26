namespace OmpOS.Harness.Terminal;

/// <summary>
/// The JSON shape of the terminal settings file. Every member is optional; a member that is absent keeps the
/// <see cref="TerminalSettings"/> default.
/// </summary>
internal sealed class TerminalSettingsFile
{
    /// <summary>Gets the explicit auto-run opt-in. Only the JSON literal <c>true</c> turns auto-run on.</summary>
    public bool? AutoRun { get; init; }

    /// <summary>Gets the largest number of characters kept for each of standard output and standard error.</summary>
    public int? MaxOutputCharacters { get; init; }
}
