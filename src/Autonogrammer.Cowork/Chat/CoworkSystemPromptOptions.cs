using System.Text;

namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// Budgets for the Cowork system prompt. A section over its budget, or a total over 8 KiB, is refused.
/// </summary>
public static class CoworkSystemPromptOptions
{
    /// <summary>The settings section that owns these budgets.</summary>
    public const string SectionName = "Cowork:SystemPrompt";

    /// <summary>The embedded base prompt, 4 KiB.</summary>
    public const int BaseBytes = 4 * 1024;

    /// <summary>The standing-context paragraph, 2 KiB.</summary>
    public const int StandingContextBytes = 2 * 1024;

    /// <summary>Browser guidance, 1 KiB.</summary>
    public const int BrowserGuidanceBytes = 1024;

    /// <summary>Computer guidance, 1 KiB.</summary>
    public const int ComputerGuidanceBytes = 1024;

    /// <summary>The most the written file may hold, 8 KiB.</summary>
    public const int TotalBytes = 8 * 1024;

    /// <summary>Counts UTF-8 bytes. A null section is empty.</summary>
    /// <param name="text">The section text.</param>
    /// <returns>The byte count.</returns>
    public static int Measure(string? text) => text is null ? 0 : Encoding.UTF8.GetByteCount(text);

    /// <summary>Refuses a section that exceeds its budget.</summary>
    /// <param name="text">The section text.</param>
    /// <param name="budget">The budget in bytes.</param>
    /// <param name="section">The section name, used in the refusal.</param>
    /// <exception cref="ArgumentOutOfRangeException">The section is over its budget.</exception>
    public static void RequireWithin(string? text, int budget, string section)
    {
        var bytes = Measure(text);
        if (bytes > budget)
        {
            throw new ArgumentOutOfRangeException(section, $"The {section} section is {bytes} bytes, over the {budget} byte budget.");
        }
    }
}
