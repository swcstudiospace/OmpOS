using System.Globalization;
using System.Text;
using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Lists the concrete host aliases of an ssh configuration (<c>~/.ssh/config</c> and the files it includes) for the host picker. It reads
/// only <c>Host</c> and <c>Include</c> directives; what a host resolves to is decided by ssh itself (see <see cref="SshConfigResolver"/>).
/// Wildcard patterns, negations and <c>Match</c> blocks do not produce aliases. An <c>Include</c> inside a <c>Host</c> or <c>Match</c> block is
/// followed unconditionally, so the list can name a host that ssh would not select; resolving the alias shows the truth.
/// </summary>
public sealed class SshConfigReader
{
    /// <summary>The deepest chain of <c>Include</c> files that is followed.</summary>
    public const int MaxIncludeDepth = 16;

    /// <summary>The most configuration files that are read in one listing.</summary>
    public const int MaxFiles = 256;

    /// <summary>The largest configuration file that is read, in bytes.</summary>
    public const long MaxFileBytes = 1024 * 1024;

    private readonly string _homeDirectory;

    /// <summary>Initializes a new instance of the <see cref="SshConfigReader"/> class.</summary>
    /// <param name="homeDirectory">The user's home directory, used for <c>~</c> and for the default <c>.ssh</c> directory. Null uses the current user's.</param>
    public SshConfigReader(string? homeDirectory = null) =>
        _homeDirectory = string.IsNullOrWhiteSpace(homeDirectory) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : homeDirectory;

    /// <summary>Gets the path of the user's default configuration file: <c>&lt;home&gt;/.ssh/config</c>.</summary>
    public string DefaultConfigPath => Path.Combine(_homeDirectory, ".ssh", "config");

    /// <summary>Lists the aliases of the user's default configuration file. A missing file is an empty listing, not an error.</summary>
    /// <returns>The aliases and warnings.</returns>
    public SshHostListing ListHosts() => ListHosts(DefaultConfigPath);

    /// <summary>Lists the aliases of a configuration file and its includes.</summary>
    /// <param name="configPath">The configuration file. A relative <c>Include</c> is resolved against the <c>.ssh</c> directory of the home directory, as ssh does for a user configuration.</param>
    /// <returns>The aliases and warnings. A missing main file is an empty listing.</returns>
    public SshHostListing ListHosts(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        var state = new ReadState(Path.Combine(_homeDirectory, ".ssh"));
        if (File.Exists(configPath))
        {
            ReadFile(Path.GetFullPath(configPath), 0, state);
        }

        return new SshHostListing { Hosts = state.Hosts, Warnings = state.Warnings };
    }

    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var started = false;
        var index = 0;
        while (index < line.Length)
        {
            var character = line[index];
            if (!inQuotes && !started && character == '#')
            {
                break;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                started = true;
            }
            else if (!inQuotes && (char.IsWhiteSpace(character) || character == '='))
            {
                if (started)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else
            {
                current.Append(character);
                started = true;
            }

            index++;
        }

        if (started)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static bool IsPattern(string token) =>
        token.Contains('*', StringComparison.Ordinal) || token.Contains('?', StringComparison.Ordinal) || token.StartsWith('!');

    private void ReadFile(string path, int depth, ReadState state)
    {
        if (depth > MaxIncludeDepth)
        {
            state.Warnings.Add(string.Create(CultureInfo.InvariantCulture, $"An Include chain is deeper than {MaxIncludeDepth} files; the rest is not read."));
            return;
        }

        if (!state.Visited.Add(path))
        {
            return;
        }

        if (state.Visited.Count > MaxFiles)
        {
            state.Warnings.Add(string.Create(CultureInfo.InvariantCulture, $"More than {MaxFiles} configuration files are included; the rest are not read."));
            return;
        }

        string[] lines;
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes)
            {
                state.Warnings.Add("The configuration file " + path + " is larger than 1 MiB and was not read.");
                return;
            }

            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            state.Warnings.Add("The configuration file " + path + " could not be read: " + ex.Message);
            return;
        }

        for (var number = 0; number < lines.Length; number++)
        {
            var tokens = Tokenize(lines[number]);
            if (tokens.Count < 2)
            {
                continue;
            }

            if (string.Equals(tokens[0], "Host", StringComparison.OrdinalIgnoreCase))
            {
                AddHosts(tokens, path, number + 1, state);
            }
            else if (string.Equals(tokens[0], "Include", StringComparison.OrdinalIgnoreCase))
            {
                for (var index = 1; index < tokens.Count; index++)
                {
                    foreach (var included in ExpandInclude(tokens[index], state))
                    {
                        ReadFile(included, depth + 1, state);
                    }
                }
            }
        }
    }

    private static void AddHosts(List<string> tokens, string path, int line, ReadState state)
    {
        for (var index = 1; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (IsPattern(token))
            {
                continue;
            }

            if (!SshProfileRules.IsValidAlias(token))
            {
                state.Warnings.Add(string.Create(CultureInfo.InvariantCulture, $"The host name in {path} line {line} cannot be used as an OmpOS alias and is not listed."));
                continue;
            }

            if (state.Aliases.Add(token))
            {
                state.Hosts.Add(new SshHostEntry(token, path, line));
            }
        }
    }

    private List<string> ExpandInclude(string pattern, ReadState state)
    {
        var expanded = pattern.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(_homeDirectory, pattern[2..])
            : pattern;
        var fullPattern = Path.IsPathFullyQualified(expanded) ? expanded : Path.Combine(state.SshDirectory, expanded);
        var directory = Path.GetDirectoryName(fullPattern) ?? state.SshDirectory;
        var filePattern = Path.GetFileName(fullPattern);
        if (directory.Contains('*', StringComparison.Ordinal) || directory.Contains('?', StringComparison.Ordinal))
        {
            state.Warnings.Add("An Include pattern with a wildcard in a directory name is not followed.");
            return [];
        }

        if (!filePattern.Contains('*', StringComparison.Ordinal) && !filePattern.Contains('?', StringComparison.Ordinal))
        {
            return File.Exists(fullPattern) ? [Path.GetFullPath(fullPattern)] : [];
        }

        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            var files = Directory.EnumerateFiles(directory, filePattern).Select(Path.GetFullPath).ToList();
            files.Sort(StringComparer.Ordinal);
            return files;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            state.Warnings.Add("The directory " + directory + " could not be listed: " + ex.Message);
            return [];
        }
    }

    private sealed class ReadState
    {
        public ReadState(string sshDirectory) => SshDirectory = sshDirectory;

        public string SshDirectory { get; }

        public HashSet<string> Visited { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SshHostEntry> Hosts { get; } = [];

        public List<string> Warnings { get; } = [];
    }
}
