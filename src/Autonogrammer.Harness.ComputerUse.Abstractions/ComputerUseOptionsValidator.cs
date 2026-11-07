namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Plain validator. The head composition calls <see cref="Validate"/> before it builds any service.
/// Failure strings name the option key and never the value. Nothing is checked while <see cref="ComputerUseOptions.Enabled"/> is false.
/// </summary>
public sealed class ComputerUseOptionsValidator
{
    /// <summary>Hard-denied chords the kill chord must not be set to (ADR-0035 D4).</summary>
    public static readonly IReadOnlyList<string> ForbiddenChords =
    [
        "Win+R",
        "Win+X",
        "Alt+F2",
        "Ctrl+Alt+T",
        "Cmd+Space",
        "Ctrl+Alt+Del",
        "Ctrl+Alt+Backspace",
        "Ctrl+Alt+F1",
        "Ctrl+Alt+F2",
        "Ctrl+Alt+F3",
        "Ctrl+Alt+F4",
        "Ctrl+Alt+F5",
        "Ctrl+Alt+F6",
        "Ctrl+Alt+F7",
        "Ctrl+Alt+F8",
        "Ctrl+Alt+F9",
        "Ctrl+Alt+F10",
        "Ctrl+Alt+F11",
        "Ctrl+Alt+F12",
    ];

    /// <summary>Validates <paramref name="options"/>. Returns an empty list when the feature is disabled.</summary>
    /// <param name="options">The options.</param>
    /// <returns>Failure strings that name keys and never values.</returns>
    public static IReadOnlyList<string> Validate(ComputerUseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return [];
        }

        List<string> failures = [];
        RequireRange(options.RatePerMinute, 1, 600, Key(nameof(ComputerUseOptions.RatePerMinute)), failures);
        RequireRange(options.RateBurst, 1, 60, Key(nameof(ComputerUseOptions.RateBurst)), failures);
        RequireRange(options.StepLimit, 1, 10_000, Key(nameof(ComputerUseOptions.StepLimit)), failures);
        RequireRange(options.StepContinueGrant, 1, 10_000, Key(nameof(ComputerUseOptions.StepContinueGrant)), failures);
        RequireRange(options.FailurePause, 1, 100, Key(nameof(ComputerUseOptions.FailurePause)), failures);
        RequireRange(options.WaitSecondsMax, 1, 5, Key(nameof(ComputerUseOptions.WaitSecondsMax)), failures);
        RequireRange(options.TypeCharCap, 1, 2000, Key(nameof(ComputerUseOptions.TypeCharCap)), failures);
        RequireRange(options.TakeoverPx, 1, 10_000, Key(nameof(ComputerUseOptions.TakeoverPx)), failures);
        RequireRange(options.SettleMs, 100, 2000, Key(nameof(ComputerUseOptions.SettleMs)), failures);
        RequireRange(options.LongEdgePx, 1, 8192, Key(nameof(ComputerUseOptions.LongEdgePx)), failures);
        RequireRange(options.MaxImageBytes, 1, 10_485_760, Key(nameof(ComputerUseOptions.MaxImageBytes)), failures);
        RequireRange(options.MaxImagesPerSession, 1, 10_000, Key(nameof(ComputerUseOptions.MaxImagesPerSession)), failures);
        ValidateChord(options.KillChord, failures);
        if (!Enum.IsDefined(options.PostActionFrame))
        {
            failures.Add(Key(nameof(ComputerUseOptions.PostActionFrame)) + " is not a known setting.");
        }

        return failures;
    }

    /// <summary>Builds the documented key for a property name.</summary>
    /// <param name="propertyName">The options property name.</param>
    /// <returns>The key, such as <c>Cowork:ComputerUse:RatePerMinute</c>.</returns>
    public static string Key(string propertyName) => ComputerUseOptions.SectionName + ":" + propertyName;

    private static void ValidateChord(string chord, List<string> failures)
    {
        string key = Key(nameof(ComputerUseOptions.KillChord));
        if (!string.IsNullOrWhiteSpace(chord))
        {
            string canonical = Canonical(chord);
            for (int i = 0; i < ForbiddenChords.Count; i++)
            {
                if (string.Equals(canonical, Canonical(ForbiddenChords[i]), StringComparison.Ordinal))
                {
                    failures.Add(key + " is not an allowed chord.");
                    return;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(chord) || ModifierCount(chord) < 2)
        {
            failures.Add(key + " does not contain two modifiers.");
        }

    }

    private static int ModifierCount(string chord)
    {
        string[] parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int count = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (IsModifier(parts[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static string Canonical(string chord)
    {
        string[] parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> modifiers = [];
        string key = string.Empty;
        for (int i = 0; i < parts.Length; i++)
        {
            if (IsModifier(parts[i]))
            {
                modifiers.Add(NormalizeModifier(parts[i]));
            }
            else
            {
                key = parts[i].ToUpperInvariant();
            }
        }

        modifiers.Sort(StringComparer.Ordinal);
        return string.Join('+', modifiers) + "+" + key;
    }

    private static bool IsModifier(string token) =>
        token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Control", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Alt", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Shift", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Win", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Windows", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Meta", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Super", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Cmd", StringComparison.OrdinalIgnoreCase)
        || token.Equals("Command", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeModifier(string token)
    {
        if (token.Equals("Control", StringComparison.OrdinalIgnoreCase) || token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
        {
            return "CTRL";
        }

        if (token.Equals("Win", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Windows", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Meta", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Super", StringComparison.OrdinalIgnoreCase))
        {
            return "WIN";
        }

        if (token.Equals("Cmd", StringComparison.OrdinalIgnoreCase) || token.Equals("Command", StringComparison.OrdinalIgnoreCase))
        {
            return "CMD";
        }

        if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
        {
            return "ALT";
        }

        return "SHIFT";
    }

    private static void RequireRange(int value, int min, int max, string key, List<string> failures)
    {
        if (value < min || value > max)
        {
            failures.Add(key + " is outside the allowed range.");
        }
    }
}
