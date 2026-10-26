namespace OmpOS.Presentation;

/// <summary>
/// The colour theme a head applies to the whole window. <c>System</c> follows the operating system; a head that cannot read the
/// operating-system setting shows the dark theme. Dark is the default.
/// </summary>
public enum ThemePreference
{
    /// <summary>Follow the operating-system light or dark setting.</summary>
    System,

    /// <summary>Always the light theme.</summary>
    Light,

    /// <summary>Always the dark theme.</summary>
    Dark,
}
