using System.Text;

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// Splits a shell command line into its simple commands, for matching permission tokens. This is a conservative reading, not a shell
/// parser: a line it cannot read with confidence (a substitution, a redirection, a subshell, a background job, an open quote) is
/// opaque, and an opaque line never matches an allow rule. For deny rules the same line is read more eagerly: substitutions and
/// subshells are split open, wrapper commands such as <c>sudo</c> and <c>env</c> are looked through, the command a <c>find</c> action runs is read as a command of its own, and the text after <c>sh -c</c> is read as a command line.
/// </summary>
internal static class ShellCommandAnalysis
{
    private const int MaxDepth = 4;

    private static readonly HashSet<string> Wrappers = new(StringComparer.Ordinal)
    {
        "sudo", "doas", "env", "nohup", "time", "command", "exec", "nice", "ionice", "xargs", "stdbuf", "timeout", "builtin", "setsid",
    };

    private static readonly HashSet<string> OptionValueWrappers = new(StringComparer.Ordinal) { "sudo", "doas" };

    private static readonly HashSet<string> OptionsWithValue = new(StringComparer.Ordinal) { "-u", "-g", "-h", "-p", "-C", "-D", "-R", "-T", "-U" };

    private static readonly HashSet<string> Shells = new(StringComparer.Ordinal) { "sh", "bash", "zsh", "dash", "ksh", "fish" };

    /// <summary>One read of a command line.</summary>
    /// <param name="Segments">The simple commands, each as its words with quotes removed.</param>
    /// <param name="Opaque">True when the line holds something the reader cannot follow.</param>
    internal sealed record Reading(IReadOnlyList<IReadOnlyList<string>> Segments, bool Opaque);

    /// <summary>Reads a command line for allow matching: separators split it, anything unreadable makes it opaque.</summary>
    /// <param name="command">The command line.</param>
    /// <returns>The reading.</returns>
    public static Reading ReadForAllow(string command) => Scan(command, splitSubstitutions: false);

    /// <summary>
    /// Returns the rule that allows every simple command of a reading, or null. An opaque reading and one with no simple command allow nothing.
    /// An assignment segment such as <c>CI=1</c> matches no rule, so it blocks the allow.
    /// </summary>
    /// <typeparam name="TRule">The rule type.</typeparam>
    /// <param name="reading">The reading from <see cref="ReadForAllow"/>.</param>
    /// <param name="ruleFor">Returns the rule that allows one segment's words, or null when none does.</param>
    /// <returns>The rule that allowed the first segment, when every segment is allowed; otherwise null.</returns>
    public static TRule? RuleAllowingEverySegment<TRule>(Reading reading, Func<IReadOnlyList<string>, TRule?> ruleFor)
        where TRule : class
    {
        ArgumentNullException.ThrowIfNull(ruleFor);
        if (reading.Opaque || reading.Segments.Count == 0)
        {
            return null;
        }

        TRule? first = null;
        foreach (var words in reading.Segments)
        {
            TRule? rule = IsAssignment(words[0]) ? null : ruleFor(words);
            if (rule is null)
            {
                return null;
            }

            first ??= rule;
        }

        return first;
    }

    /// <summary>Lists every command a command line may run, for deny matching: the simple commands, those inside substitutions, the ones wrappers run, and the ones a shell's <c>-c</c> text holds.</summary>
    /// <param name="command">The command line.</param>
    /// <returns>The candidate commands, each as its words.</returns>
    public static IReadOnlyList<IReadOnlyList<string>> CandidatesForDeny(string command)
    {
        var candidates = new List<IReadOnlyList<string>>();
        Expand(command, 0, candidates);
        return candidates;
    }

    /// <summary>Returns whether a word is an environment assignment such as <c>CI=1</c>.</summary>
    /// <param name="word">The word.</param>
    /// <returns>True when it has the form <c>NAME=value</c>.</returns>
    public static bool IsAssignment(string word)
    {
        var equals = word.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0 || !(char.IsLetter(word[0]) || word[0] == '_'))
        {
            return false;
        }

        for (var index = 1; index < equals; index++)
        {
            if (!(char.IsLetterOrDigit(word[index]) || word[index] == '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the last path component of a command word.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The word after the last separator.</returns>
    public static string BaseName(string word)
    {
        var slash = word.LastIndexOfAny(['/', '\\']);
        return slash < 0 ? word : word[(slash + 1)..];
    }

    private static void Expand(string command, int depth, List<IReadOnlyList<string>> into)
    {
        foreach (var segment in Scan(command, splitSubstitutions: true).Segments)
        {
            ExpandWords(segment, depth, into);
        }
    }

    private static void ExpandWords(IReadOnlyList<string> words, int depth, List<IReadOnlyList<string>> into)
    {
        var start = 0;
        while (start < words.Count && IsAssignment(words[start]))
        {
            start++;
        }

        if (start >= words.Count)
        {
            return;
        }

        var current = words.Skip(start).ToArray();
        into.Add(current);
        if (depth >= MaxDepth)
        {
            return;
        }

        var name = BaseName(current[0]);
        if (Wrappers.Contains(name))
        {
            var index = 1;
            while (index < current.Length && IsWrapperNoise(name, current[index]))
            {
                index += OptionValueWrappers.Contains(name) && OptionsWithValue.Contains(current[index]) ? 2 : 1;
            }

            if (index < current.Length)
            {
                ExpandWords(current[index..], depth + 1, into);
            }
        }
        else if (Shells.Contains(name))
        {
            for (var index = 1; index < current.Length - 1; index++)
            {
                if (current[index].Length > 1 && current[index][0] == '-' && current[index][1] != '-' && current[index].Contains('c', StringComparison.Ordinal))
                {
                    Expand(current[index + 1], depth + 1, into);
                    break;
                }
            }
        }
        else if (string.Equals(name, "find", StringComparison.Ordinal))
        {
            ExpandFindActions(current, depth, into);
        }
    }

    private static void ExpandFindActions(string[] words, int depth, List<IReadOnlyList<string>> into)
    {
        for (var index = 1; index < words.Length; index++)
        {
            if (words[index] is not ("-exec" or "-execdir" or "-ok" or "-okdir"))
            {
                continue;
            }

            var end = index + 1;
            while (end < words.Length && !IsFindTerminator(words, end))
            {
                end++;
            }

            if (end > index + 1)
            {
                ExpandWords(words[(index + 1)..end], depth + 1, into);
            }

            index = end;
        }
    }

    private static bool IsFindTerminator(string[] words, int index) =>
        string.Equals(words[index], ";", StringComparison.Ordinal) || (string.Equals(words[index], "+", StringComparison.Ordinal) && string.Equals(words[index - 1], "{}", StringComparison.Ordinal));

    private static bool IsWrapperNoise(string wrapper, string word) =>
        (word.Length > 1 && word[0] == '-') || IsAssignment(word) || (wrapper is "timeout" or "nice" or "ionice" && IsDuration(word));

    private static bool IsDuration(string word)
    {
        if (word.Length == 0)
        {
            return false;
        }

        var digits = word.Length;
        if (word[^1] is 's' or 'm' or 'h' or 'd')
        {
            digits--;
        }

        return digits > 0 && word.AsSpan(0, digits).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    private static Reading Scan(string command, bool splitSubstitutions) => new Scanner(command, splitSubstitutions).Run();

    /// <summary>One pass over a command line: splits it into segments of words and notes whether anything in it cannot be read statically.</summary>
    private sealed class Scanner(string command, bool splitSubstitutions)
    {
        private readonly List<IReadOnlyList<string>> _segments = [];
        private readonly StringBuilder _word = new();
        private List<string> _words = [];
        private bool _hasWord;
        private bool _opaque;
        private char _quote;
        private int _index;

        public Reading Run()
        {
            for (_index = 0; _index < command.Length; _index++)
            {
                var c = command[_index];
                var next = _index + 1 < command.Length ? command[_index + 1] : '\0';
                if (_quote == '\'')
                {
                    InSingleQuotes(c);
                }
                else if (_quote == '"')
                {
                    InDoubleQuotes(c, next);
                }
                else
                {
                    Unquoted(c, next);
                }
            }

            if (_quote != '\0')
            {
                _opaque = true;
            }

            EndSegment();
            return new Reading(_segments, _opaque);
        }

        private void EndWord()
        {
            if (_hasWord)
            {
                _words.Add(_word.ToString());
            }

            _word.Clear();
            _hasWord = false;
        }

        private void EndSegment()
        {
            EndWord();
            if (_words.Count > 0)
            {
                _segments.Add(_words);
                _words = [];
            }
        }

        private void Append(char c)
        {
            _word.Append(c);
            _hasWord = true;
        }

        private void InSingleQuotes(char c)
        {
            if (c == '\'')
            {
                _quote = '\0';
            }
            else
            {
                _word.Append(c);
            }
        }

        private void InDoubleQuotes(char c, char next)
        {
            if (c == '"')
            {
                _quote = '\0';
            }
            else if (c == '\\' && next != '\0')
            {
                _word.Append(next);
                _index++;
            }
            else if (c == '`' || (c == '$' && next == '('))
            {
                _opaque = true;
                if (!splitSubstitutions)
                {
                    _word.Append(c);
                    return;
                }

                EndSegment();
                _quote = '\0';
                if (c == '$')
                {
                    _index++;
                }
            }
            else
            {
                _word.Append(c);
            }
        }

        private void Unquoted(char c, char next)
        {
            switch (c)
            {
                case '\'' or '"':
                    _quote = c;
                    _hasWord = true;
                    break;
                case '\\':
                    if (next != '\0')
                    {
                        Append(next);
                        _index++;
                    }

                    break;
                case ' ' or '\t' or '\r':
                    EndWord();
                    break;
                case '\n' or ';':
                    EndSegment();
                    break;
                case '&' or '|':
                    Chain(c, next);
                    break;
                case '>' or '<':
                    _opaque = true;
                    Append(c);
                    break;
                case '`' or '(' or ')':
                    Substitution(c, skipNext: false);
                    break;
                case '$' when next == '(':
                    Substitution(c, skipNext: true);
                    break;
                default:
                    Append(c);
                    break;
            }
        }

        private void Chain(char c, char next)
        {
            EndSegment();
            if (c == '&')
            {
                if (next == '&')
                {
                    _index++;
                }
                else
                {
                    _opaque = true;
                }
            }
            else if (next is '|' or '&')
            {
                _index++;
            }
        }

        private void Substitution(char c, bool skipNext)
        {
            _opaque = true;
            if (!splitSubstitutions)
            {
                Append(c);
                return;
            }

            EndSegment();
            if (skipNext)
            {
                _index++;
            }
        }
    }
}
