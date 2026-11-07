namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Reads <see cref="ComputerUseOptions"/> from environment variables named by the key path with each colon replaced by
/// a double underscore (<c>Cowork__ComputerUse__RatePerMinute</c>). Absent variables keep defaults. Unparsable values
/// are reported by key and never echoed. The per-user settings overlay is applied by the head, not here.
/// </summary>
public sealed class ComputerUseOptionsReader
{
    /// <summary>Reads options from <paramref name="getVariable"/>.</summary>
    /// <param name="getVariable">Variable reader. <see langword="null"/> means the variable is absent.</param>
    /// <returns>Options and key-only failures.</returns>
    public static ComputerUseOptionsReadResult Read(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        ComputerUseOptions options = new();
        List<string> failures = [];
        ReadBool(getVariable, nameof(ComputerUseOptions.Enabled), value => options.Enabled = value, failures);
        ReadBool(getVariable, nameof(ComputerUseOptions.AllowDispatchSessions), value => options.AllowDispatchSessions = value, failures);
        ReadString(getVariable, nameof(ComputerUseOptions.KillChord), value => options.KillChord = value);
        ReadInt(getVariable, nameof(ComputerUseOptions.RatePerMinute), value => options.RatePerMinute = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.RateBurst), value => options.RateBurst = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.StepLimit), value => options.StepLimit = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.StepContinueGrant), value => options.StepContinueGrant = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.FailurePause), value => options.FailurePause = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.WaitSecondsMax), value => options.WaitSecondsMax = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.TypeCharCap), value => options.TypeCharCap = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.TakeoverPx), value => options.TakeoverPx = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.SettleMs), value => options.SettleMs = value, failures);
        ReadEnum(getVariable, nameof(ComputerUseOptions.PostActionFrame), value => options.PostActionFrame = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.LongEdgePx), value => options.LongEdgePx = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.MaxImageBytes), value => options.MaxImageBytes = value, failures);
        ReadInt(getVariable, nameof(ComputerUseOptions.MaxImagesPerSession), value => options.MaxImagesPerSession = value, failures);
        ReadBool(getVariable, nameof(ComputerUseOptions.AuditThumbnails), value => options.AuditThumbnails = value, failures);
        ReadOverrides(getVariable, options, failures);
        foreach (string failure in ComputerUseOptionsValidator.Validate(options))
        {
            failures.Add(failure);
        }

        return new ComputerUseOptionsReadResult(options, failures);
    }

    /// <summary>Environment variable name for a property.</summary>
    /// <param name="propertyName">The options property name.</param>
    /// <returns>The variable name, such as <c>Cowork__ComputerUse__RatePerMinute</c>.</returns>
    public static string VariableName(string propertyName) =>
        ComputerUseOptions.SectionName.Replace(":", "__", StringComparison.Ordinal) + "__" + propertyName;

    private static void ReadString(Func<string, string?> getVariable, string propertyName, Action<string> assign)
    {
        string? raw = getVariable(VariableName(propertyName));
        if (!string.IsNullOrEmpty(raw))
        {
            assign(raw);
        }
    }

    private static void ReadBool(Func<string, string?> getVariable, string propertyName, Action<bool> assign, List<string> failures)
    {
        string? raw = getVariable(VariableName(propertyName));
        if (raw is null)
        {
            return;
        }

        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            assign(true);
            return;
        }

        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            assign(false);
            return;
        }

        failures.Add(ComputerUseOptionsValidator.Key(propertyName) + " is not a valid boolean.");
    }

    private static void ReadInt(Func<string, string?> getVariable, string propertyName, Action<int> assign, List<string> failures)
    {
        string? raw = getVariable(VariableName(propertyName));
        if (raw is null)
        {
            return;
        }

        if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
        {
            assign(value);
            return;
        }

        failures.Add(ComputerUseOptionsValidator.Key(propertyName) + " is not a valid integer.");
    }

    private static void ReadEnum(Func<string, string?> getVariable, string propertyName, Action<PostActionFrame> assign, List<string> failures)
    {
        string? raw = getVariable(VariableName(propertyName));
        if (raw is null)
        {
            return;
        }

        if (Enum.TryParse(raw, ignoreCase: true, out PostActionFrame value) && Enum.IsDefined(value))
        {
            assign(value);
            return;
        }

        failures.Add(ComputerUseOptionsValidator.Key(propertyName) + " is not a known setting.");
    }

    private static void ReadOverrides(Func<string, string?> getVariable, ComputerUseOptions options, List<string> failures)
    {
        string? raw = getVariable(VariableName(nameof(ComputerUseOptions.ModelOverrides)));
        if (raw is null)
        {
            return;
        }

        Dictionary<string, int> table = new(StringComparer.Ordinal);
        string[] pairs = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < pairs.Length; i++)
        {
            string[] parts = pairs[i].Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || parts[0].Length == 0
                || !int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int edge))
            {
                failures.Add(ComputerUseOptionsValidator.Key(nameof(ComputerUseOptions.ModelOverrides)) + " is not a valid table.");
                return;
            }

            table[parts[0]] = edge;
        }

        options.ReplaceModelOverrides(table);
    }
}
