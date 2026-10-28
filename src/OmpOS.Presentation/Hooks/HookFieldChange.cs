namespace OmpOS.Presentation.Hooks;

/// <summary>One field of a hook that differs from the version you approved.</summary>
/// <param name="Field">The name of the field, for example <c>Command</c> or <c>Timeout</c>.</param>
/// <param name="Before">The value you approved.</param>
/// <param name="After">The value the hook has now.</param>
public sealed record HookFieldChange(string Field, string Before, string After)
{
    /// <summary>Gets the change as one line: the field, the approved value and the current value.</summary>
    public string Text => $"{Field}: {Before} -> {After}";
}
