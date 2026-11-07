#pragma warning disable MA0048 // Verdict and decision share the policy file the plan owns.
using System.Globalization;
using System.Text.RegularExpressions;

namespace Autonogrammer.Cowork.Memory;

/// <summary>What the policy decided.</summary>
public enum MemoryPolicyVerdict
{
    /// <summary>The text may be stored.</summary>
    Allowed = 0,

    /// <summary>A class that is never stored.</summary>
    RefusedNeverSave = 1,

    /// <summary>A sensitive class, stored only when include_sensitive is on.</summary>
    RefusedSensitive = 2,
}

/// <summary>A policy answer. <see cref="Sentence"/> is for the agent and does not repeat the text.</summary>
/// <param name="Verdict">The decision.</param>
/// <param name="ClassName">The class, or null when allowed.</param>
/// <param name="Sentence">The refusal sentence, or null when allowed.</param>
public sealed record MemoryPolicyDecision(MemoryPolicyVerdict Verdict, string? ClassName, string? Sentence);

/// <summary>
/// Decides before every write. Detectors are local: Luhn, IBAN mod-97, national-id patterns and English lexicons.
/// This type does not open a network connection, log content, or use a vector type.
/// </summary>
public static partial class MemoryContentPolicy
{
    /// <summary>Government identification numbers.</summary>
    public const string GovernmentId = "GovernmentId";

    /// <summary>Criminal history.</summary>
    public const string CriminalHistory = "CriminalHistory";

    /// <summary>Financial account numbers.</summary>
    public const string FinancialAccount = "FinancialAccount";

    /// <summary>Immigration status.</summary>
    public const string ImmigrationStatus = "ImmigrationStatus";

    /// <summary>Health.</summary>
    public const string Health = "Health";

    /// <summary>Race and ethnicity.</summary>
    public const string RaceEthnicity = "RaceEthnicity";

    /// <summary>Religious belief.</summary>
    public const string ReligiousBelief = "ReligiousBelief";

    /// <summary>Politics.</summary>
    public const string Politics = "Politics";

    /// <summary>Gender identity.</summary>
    public const string GenderIdentity = "GenderIdentity";

    private static readonly string[] CriminalPhrases = ["criminal history", "criminal record", "felony conviction", "convicted of", "arrest record"];

    private static readonly string[] ImmigrationPhrases = ["immigration status", "undocumented immigrant", "visa overstay", "deportation order", "asylum application"];

    private static readonly string[] HealthPhrases = ["medical diagnosis", "blood pressure", "prescription medication", "hiv status"];

    private static readonly string[] RacePhrases = ["my ethnicity", "my race is", "racial identity"];

    private static readonly string[] ReligionPhrases = ["religious belief", "my religion"];

    private static readonly string[] PoliticsPhrases = ["political party", "my politics"];

    private static readonly string[] GenderPhrases = ["gender identity", "transgender", "non-binary", "nonbinary"];

    /// <summary>Returns a sentence when a field is over its named cap, or null when the lengths are allowed.</summary>
    /// <param name="topic">The topic.</param>
    /// <param name="content">The content.</param>
    /// <returns>The sentence, or null.</returns>
    public static string? LimitSentence(string? topic, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > MemoryOptions.MaxContentChars)
        {
            return "This was not saved. Content is limited to " + MemoryOptions.MaxContentChars.ToString(CultureInfo.InvariantCulture) + " characters.";
        }

        if (topic is not null && topic.Length > MemoryOptions.MaxTopicChars)
        {
            return "This was not saved. Topic is limited to " + MemoryOptions.MaxTopicChars.ToString(CultureInfo.InvariantCulture) + " characters.";
        }

        return null;
    }

    /// <summary>Decides whether the draft may be stored.</summary>
    /// <param name="topic">The topic.</param>
    /// <param name="content">The content.</param>
    /// <param name="category">The category, or null.</param>
    /// <param name="includeSensitive">Whether sensitive categories are opted in.</param>
    /// <returns>The decision.</returns>
    public static MemoryPolicyDecision Evaluate(string? topic, string content, string? category, bool includeSensitive)
    {
        ArgumentNullException.ThrowIfNull(content);
        var text = (topic ?? string.Empty) + "\n" + content;
        var never = NeverSaveClass(text, category);
        if (never is not null)
        {
            return new MemoryPolicyDecision(MemoryPolicyVerdict.RefusedNeverSave, never, NeverSaveSentence(never));
        }

        var sensitive = SensitiveClass(text, category);
        if (sensitive is not null && !includeSensitive)
        {
            return new MemoryPolicyDecision(MemoryPolicyVerdict.RefusedSensitive, sensitive, SensitiveSentence(sensitive));
        }

        return new MemoryPolicyDecision(MemoryPolicyVerdict.Allowed, null, null);
    }

    private static string? NeverSaveClass(string text, string? category)
    {
        if (IsGovernmentId(text) || CategoryIs(category, GovernmentId, "government_id"))
        {
            return GovernmentId;
        }

        if (HasPhrase(text, CriminalPhrases) || CategoryIs(category, CriminalHistory, "criminal_history"))
        {
            return CriminalHistory;
        }

        if (HasFinancialNumber(text) || CategoryIs(category, FinancialAccount, "financial_account"))
        {
            return FinancialAccount;
        }

        if (HasPhrase(text, ImmigrationPhrases) || CategoryIs(category, ImmigrationStatus, "immigration_status"))
        {
            return ImmigrationStatus;
        }

        return null;
    }

    private static string? SensitiveClass(string text, string? category)
    {
        if (HasPhrase(text, HealthPhrases) || CategoryIs(category, Health, "health"))
        {
            return Health;
        }

        if (HasPhrase(text, RacePhrases) || CategoryIs(category, RaceEthnicity, "race_ethnicity"))
        {
            return RaceEthnicity;
        }

        if (HasPhrase(text, ReligionPhrases) || CategoryIs(category, ReligiousBelief, "religious_belief"))
        {
            return ReligiousBelief;
        }

        if (HasPhrase(text, PoliticsPhrases) || CategoryIs(category, Politics, "politics"))
        {
            return Politics;
        }

        if (HasPhrase(text, GenderPhrases) || CategoryIs(category, GenderIdentity, "gender_identity"))
        {
            return GenderIdentity;
        }

        return null;
    }

    private static bool CategoryIs(string? category, string name, string wire)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        return string.Equals(category, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, wire, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPhrase(string text, string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGovernmentId(string text)
    {
        foreach (Match match in SsnPattern().Matches(text))
        {
            var area = int.Parse(match.Groups["area"].Value, CultureInfo.InvariantCulture);
            var group = int.Parse(match.Groups["group"].Value, CultureInfo.InvariantCulture);
            var serial = int.Parse(match.Groups["serial"].Value, CultureInfo.InvariantCulture);
            if (area is not (0 or 666) && area < 900 && group != 0 && serial != 0)
            {
                return true;
            }
        }

        return NinoPattern().IsMatch(text);
    }

    private static bool HasFinancialNumber(string text)
    {
        foreach (Match match in IbanPattern().Matches(text))
        {
            if (IbanChecks(match.Value))
            {
                return true;
            }
        }

        return HasLuhnRun(text);
    }

    private static bool HasLuhnRun(string text)
    {
        var digits = new List<char>(19);
        var inRun = false;
        for (var i = 0; i <= text.Length; i++)
        {
            var end = i == text.Length;
            var ch = end ? '\0' : text[i];
            if (!end && char.IsDigit(ch))
            {
                if (digits.Count == 19)
                {
                    digits.Clear();
                    inRun = false;
                    continue;
                }

                inRun = true;
                digits.Add(ch);
                continue;
            }

            if (!end && inRun && ch is ' ' or '-')
            {
                continue;
            }

            if (inRun && digits.Count is >= 13 and <= 19 && Luhn(digits))
            {
                return true;
            }

            digits.Clear();
            inRun = false;
        }

        return false;
    }

    private static bool Luhn(List<char> digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Count - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n -= 9;
                }
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    private static bool IbanChecks(string raw)
    {
        var compact = raw.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (compact.Length is < 15 or > 34)
        {
            return false;
        }

        var rearranged = string.Concat(compact.AsSpan(4), compact.AsSpan(0, 4));
        var remainder = 0;
        foreach (var ch in rearranged)
        {
            var piece = char.IsDigit(ch) ? ((int)(ch - '0')).ToString(CultureInfo.InvariantCulture) : ((int)(ch - 'A' + 10)).ToString(CultureInfo.InvariantCulture);
            foreach (var digit in piece)
            {
                remainder = ((remainder * 10) + (digit - '0')) % 97;
            }
        }

        return remainder == 1;
    }

    private static string NeverSaveSentence(string className) => className switch
    {
        GovernmentId => "This was not saved. Government ID numbers are never stored.",
        CriminalHistory => "This was not saved. Criminal history is never stored.",
        FinancialAccount => "This was not saved. Financial account numbers are never stored.",
        _ => "This was not saved. Immigration status is never stored.",
    };

    private static string SensitiveSentence(string className) => className switch
    {
        Health => "This was not saved. Health is stored only when include_sensitive is on.",
        RaceEthnicity => "This was not saved. Race and ethnicity are stored only when include_sensitive is on.",
        ReligiousBelief => "This was not saved. Religious belief is stored only when include_sensitive is on.",
        Politics => "This was not saved. Politics are stored only when include_sensitive is on.",
        _ => "This was not saved. Gender identity is stored only when include_sensitive is on.",
    };

    [GeneratedRegex(@"\b(?<area>\d{3})-(?<group>\d{2})-(?<serial>\d{4})\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex SsnPattern();

    [GeneratedRegex(@"\b[A-CEGHJ-PR-TW-Z]{2}\s?\d{2}\s?\d{2}\s?\d{2}\s?[A-D]\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex NinoPattern();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{11,30}\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex IbanPattern();
}
