using System.Globalization;
using System.Text;

namespace OmpOS.Workspace.Ignore;

/// <summary>
/// Decides which workspace paths Cursor-style ignore files hide from the agent, from Tab and from <c>@</c> mentions. Rules use gitignore syntax and rank, lowest first: the built-in list
/// (<see cref="IgnoreDefaults"/>), the global list (<see cref="IgnoreOptions.GlobalPatterns"/>), then the ignore files from the workspace root down to the deepest directory (a deeper file
/// overrides a shallower one), and within one directory <c>.gitignore</c> first and the <see cref="IgnoreOptions.IgnoreFileNames"/> in their order. The last matching rule wins, and a rule starting
/// with <c>!</c> re-includes what an earlier rule ignored, except below an ignored folder: nothing can be re-included once a parent folder is ignored (the gitignore rule).
/// Paths are workspace-relative with <c>/</c> separators; a path that is rooted, climbs out with <c>..</c>, holds a NUL character or is deeper than <see cref="IgnoreOptions.MaxPathDepth"/> is reported as ignored.
/// </summary>
/// <remarks>
/// Thread safety: after loading, any number of threads may call <see cref="IsIgnored"/> and <see cref="Explain"/> at once. <see cref="AddFile(string, string)"/>, <see cref="AddFile(string, string, string)"/>,
/// <see cref="TryLoadHierarchy"/> and the property <see cref="Diagnostics"/> change or read the loading state and must not race with a query or with each other. A lazy tree walk adds the files of a
/// directory before it queries the entries below it, on one thread.
/// </remarks>
public sealed class IgnoreMatcher
{
    private const string DefaultListSource = "built-in default list";
    private const string GlobalListSource = "global ignore list";
    private const string DefaultFileName = ".cursorignore";

    private readonly IgnoreOptions _options;
    private readonly string[] _fileNames;
    private readonly Dictionary<string, List<IgnoreRule>> _levels;
    private readonly List<IgnoreRule> _defaultRules;
    private readonly List<IgnoreRule> _globalRules;
    private readonly List<IgnoreDiagnostic> _diagnostics = [];

    private IgnoreMatcher(IgnoreOptions options)
    {
        _options = options;
        _levels = new Dictionary<string, List<IgnoreRule>>(options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        _fileNames = BuildFileNames(options);
        _defaultRules = options.UseDefaultList
            ? IgnoreRuleReader.ReadLines(IgnoreDefaults.Patterns, DefaultListSource, options, _diagnostics)
            : new List<IgnoreRule>();
        _globalRules = IgnoreRuleReader.ReadLines(options.GlobalPatterns, GlobalListSource, options, _diagnostics);
    }

    /// <summary>Gets the problems found while loading: skipped lines, skipped files, symbolic links that were not followed and unsafe directories. The list grows as files are added.</summary>
    public IReadOnlyList<IgnoreDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Gets the options this matcher was built with.</summary>
    public IgnoreOptions Options => _options;

    /// <summary>Creates a matcher that holds the built-in list and the global list but no ignore file yet.</summary>
    /// <param name="options">The settings.</param>
    /// <returns>The matcher; add ignore files with <see cref="AddFile(string, string)"/> or <see cref="TryLoadHierarchy"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static IgnoreMatcher Create(IgnoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new IgnoreMatcher(options);
    }

    /// <summary>Creates a matcher and loads the ignore files of the given directories.</summary>
    /// <param name="source">Where the ignore files are read from.</param>
    /// <param name="directoriesToScan">The directories to read, relative to the workspace root (<c>""</c> is the root itself).</param>
    /// <param name="options">The settings.</param>
    /// <returns>The loaded matcher.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IgnoreMatcher Load(IIgnoreFileSource source, IEnumerable<string> directoriesToScan, IgnoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(directoriesToScan);
        var matcher = Create(options);
        foreach (var directory in directoriesToScan)
        {
            matcher.TryLoadHierarchy(source, directory);
        }

        return matcher;
    }

    /// <summary>Adds the rules of one ignore file whose text the caller already has, labelled as the default ignore file name in that directory.</summary>
    /// <param name="directoryRelative">The directory that holds the file, relative to the workspace root (<c>""</c> is the root). Its rules apply to that directory's subtree.</param>
    /// <param name="text">The file text.</param>
    /// <returns>The number of rules added; 0 when the directory is unsafe (a diagnostic says so) or the file holds no rule.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public int AddFile(string directoryRelative, string text)
    {
        ArgumentNullException.ThrowIfNull(directoryRelative);
        return AddFile(directoryRelative, text, Combine(directoryRelative.Trim('/'), DefaultFileName));
    }

    /// <summary>Adds the rules of one ignore file whose text the caller already has.</summary>
    /// <param name="directoryRelative">The directory that holds the file, relative to the workspace root (<c>""</c> is the root). Its rules apply to that directory's subtree.</param>
    /// <param name="text">The file text.</param>
    /// <param name="sourceName">The name shown in diagnostics and <see cref="IgnoreDecision.Source"/>.</param>
    /// <returns>The number of rules added; 0 when the directory is unsafe (a diagnostic says so) or the file holds no rule.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public int AddFile(string directoryRelative, string text, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(directoryRelative);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sourceName);
        if (!IgnorePath.TryNormalize(directoryRelative, _options.MaxPathDepth, out var segments))
        {
            _diagnostics.Add(new IgnoreDiagnostic(sourceName, 0, "The directory is not a safe workspace-relative path; the file was skipped."));
            return 0;
        }

        return AddRules(string.Join('/', segments), text, sourceName);
    }

    /// <summary>Reads every configured ignore file in one directory (<c>.gitignore</c> first when it is respected) and adds its rules. A file behind a symbolic link or over the size cap is skipped with a diagnostic.</summary>
    /// <param name="source">Where the ignore files are read from.</param>
    /// <param name="directoryRelative">The directory, relative to the workspace root (<c>""</c> is the root).</param>
    /// <returns><see langword="true"/> when at least one ignore file was read.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public bool TryLoadHierarchy(IIgnoreFileSource source, string directoryRelative)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(directoryRelative);
        if (!IgnorePath.TryNormalize(directoryRelative, _options.MaxPathDepth, out var segments))
        {
            _diagnostics.Add(new IgnoreDiagnostic(directoryRelative, 0, "The directory is not a safe workspace-relative path; its ignore files were skipped."));
            return false;
        }

        var directory = string.Join('/', segments);
        var loaded = false;
        foreach (var name in _fileNames)
        {
            loaded |= LoadFile(source, directory, name);
        }

        return loaded;
    }

    /// <summary>Asks whether a path is hidden.</summary>
    /// <param name="relativePath">The path relative to the workspace root, with <c>/</c> separators. The empty path is the workspace root, which is never ignored.</param>
    /// <param name="isDirectory">Whether the path is a folder (a rule ending in <c>/</c> matches folders only).</param>
    /// <returns><see langword="true"/> when the path or any folder above it is ignored, or when the path is unsafe.</returns>
    public bool IsIgnored(string relativePath, bool isDirectory) => Explain(relativePath, isDirectory).Ignored;

    /// <summary>Asks whether a path is hidden and which rule decided it.</summary>
    /// <param name="relativePath">The path relative to the workspace root, with <c>/</c> separators.</param>
    /// <param name="isDirectory">Whether the path is a folder.</param>
    /// <returns>The decision with the deciding rule and its file; for a path below an ignored folder, the rule that ignored the folder.</returns>
    public IgnoreDecision Explain(string relativePath, bool isDirectory)
    {
        if (!IgnorePath.TryNormalize(relativePath, _options.MaxPathDepth, out var segments))
        {
            return IgnoreDecision.UnsafePath;
        }

        if (segments.Length == 0)
        {
            return IgnoreDecision.NotIgnored;
        }

        string[] keys = _levels.Count == 0 ? Array.Empty<string>() : BuildKeys(segments);
        for (var length = 1; length < segments.Length; length++)
        {
            var parentRule = FindRule(segments, length, isDirectory: true, keys);
            if (parentRule is { IsNegation: false })
            {
                return Decide(parentRule);
            }
        }

        var rule = FindRule(segments, segments.Length, isDirectory, keys);
        return rule is null ? IgnoreDecision.NotIgnored : Decide(rule);
    }

    private static string[] BuildFileNames(IgnoreOptions options)
    {
        var names = new List<string>();
        if (options.RespectGitIgnore)
        {
            names.Add(".gitignore");
        }

        foreach (var name in options.IgnoreFileNames)
        {
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return [.. names];
    }

    private static string Combine(string directory, string name) => directory.Length == 0 ? name : directory + "/" + name;

    private static string[] BuildKeys(string[] segments)
    {
        var keys = new string[segments.Length];
        keys[0] = string.Empty;
        for (var depth = 1; depth < segments.Length; depth++)
        {
            keys[depth] = depth == 1 ? segments[0] : keys[depth - 1] + "/" + segments[depth - 1];
        }

        return keys;
    }

    private static IgnoreDecision Decide(IgnoreRule rule) => new(!rule.IsNegation, rule.Pattern, rule.Source, rule.Line);

    private bool LoadFile(IIgnoreFileSource source, string directory, string name)
    {
        var path = Combine(directory, name);
        if (!IgnorePath.TryNormalize(path, _options.MaxPathDepth, out _))
        {
            _diagnostics.Add(new IgnoreDiagnostic(path, 0, "The ignore file name is not a safe workspace-relative path; it was skipped."));
            return false;
        }

        if (source.IsSymbolicLink(path))
        {
            _diagnostics.Add(new IgnoreDiagnostic(path, 0, "The ignore file is a symbolic link, or lies behind one, and was not followed."));
            return false;
        }

        var text = source.ReadText(path, _options.MaxFileBytes);
        if (text is null)
        {
            return false;
        }

        if (Encoding.UTF8.GetByteCount(text) > _options.MaxFileBytes)
        {
            _diagnostics.Add(new IgnoreDiagnostic(path, 0, string.Create(CultureInfo.InvariantCulture, $"The ignore file is larger than {_options.MaxFileBytes} bytes and was skipped.")));
            return false;
        }

        AddRules(directory, text, path);
        return true;
    }

    private int AddRules(string directoryKey, string text, string source)
    {
        var rules = IgnoreRuleReader.ReadText(text, source, _options, _diagnostics);
        if (rules.Count == 0)
        {
            return 0;
        }

        if (!_levels.TryGetValue(directoryKey, out var level))
        {
            level = [];
            _levels[directoryKey] = level;
        }

        level.AddRange(rules);
        return rules.Count;
    }

    private IgnoreRule? FindRule(string[] segments, int length, bool isDirectory, string[] keys)
    {
        for (var depth = keys.Length == 0 ? -1 : length - 1; depth >= 0; depth--)
        {
            if (_levels.TryGetValue(keys[depth], out var rules))
            {
                var hit = LastMatch(rules, new ReadOnlySpan<string>(segments, depth, length - depth), isDirectory);
                if (hit is not null)
                {
                    return hit;
                }
            }
        }

        return LastMatch(_globalRules, new ReadOnlySpan<string>(segments, 0, length), isDirectory)
            ?? LastMatch(_defaultRules, new ReadOnlySpan<string>(segments, 0, length), isDirectory);
    }

    private IgnoreRule? LastMatch(List<IgnoreRule> rules, ReadOnlySpan<string> path, bool isDirectory)
    {
        for (var index = rules.Count - 1; index >= 0; index--)
        {
            var rule = rules[index];
            var result = rule.Matches(path, isDirectory, _options.MaxMatchWork);
            if (result == IgnoreMatchResult.Match || (result == IgnoreMatchResult.CapExceeded && !rule.IsNegation))
            {
                return rule;
            }
        }

        return null;
    }
}
