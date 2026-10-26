namespace OmpOS.Workspace;

/// <summary>
/// The file layout and validation rules of the workspace convention (ADR-0005).
/// </summary>
public static class WorkspaceConventions
{
    /// <summary>The folder that holds all convention files, relative to the workspace root.</summary>
    public const string ConventionFolder = ".ompos";

    /// <summary>The folder of rule files (<c>*.md</c>), relative to the workspace root.</summary>
    public const string RulesFolder = ".ompos/rules";

    /// <summary>The folder of skill files (<c>*.md</c>), relative to the workspace root.</summary>
    public const string SkillsFolder = ".ompos/skills";

    /// <summary>The folder of mode files (<c>*.json</c>), relative to the workspace root.</summary>
    public const string ModesFolder = ".ompos/modes";

    /// <summary>The setup file, relative to the workspace root.</summary>
    public const string SetupFile = ".ompos/setup.json";

    /// <summary>The largest convention file that is read. Larger files are reported as diagnostics.</summary>
    public const int MaxConventionFileBytes = 256 * 1024;

    /// <summary>The longest accepted mode display name, in characters.</summary>
    public const int MaxDisplayNameLength = 100;

    private const int MaxIdLength = 64;

    /// <summary>
    /// Returns whether an identifier is a valid rule, skill or mode id: 1 to 64 characters, lowercase ASCII letters,
    /// digits, '-' and '_', starting with a letter or digit.
    /// </summary>
    /// <param name="id">The candidate identifier.</param>
    public static bool IsValidId(string? id)
    {
        if (id is null || id.Length == 0 || id.Length > MaxIdLength || !IsLowerAsciiLetterOrDigit(id[0]))
        {
            return false;
        }

        foreach (var character in id)
        {
            if (!IsLowerAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns whether a name is a valid setup environment variable name: ASCII letters, digits and '_',
    /// not starting with a digit.
    /// </summary>
    /// <param name="name">The candidate variable name.</param>
    public static bool IsValidEnvironmentName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || !(IsAsciiLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!(IsAsciiLetter(character) || IsAsciiDigit(character) || character == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLowerAsciiLetterOrDigit(char character) =>
        character is (>= 'a' and <= 'z') or (>= '0' and <= '9');

    private static bool IsAsciiLetter(char character) =>
        character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static bool IsAsciiDigit(char character) =>
        character is >= '0' and <= '9';
}
