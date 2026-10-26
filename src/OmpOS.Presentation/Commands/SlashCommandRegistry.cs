using System.Collections.ObjectModel;
using OmpOS.Contracts.Intents;
using OmpOS.Parser;
using OmpOS.Parser.Recognizers;

namespace OmpOS.Presentation.Commands;

/// <summary>
/// An immutable, validated set of slash-command descriptors. It lists commands in ordinal name order, matches names by prefix,
/// suggests close names for an unknown one, resolves a name to its availability, and splits typed input through the existing
/// <see cref="SlashCommandRecognizer"/>.
/// The registry does not filter by platform: every listing, match, suggestion and resolution includes a command whatever its
/// <see cref="SlashCommandDescriptor.Platforms"/>. The host applies those platforms when it decides what to show or run.
/// </summary>
public sealed class SlashCommandRegistry
{
    /// <summary>The largest number of names that <see cref="Suggest"/> returns.</summary>
    public const int MaxSuggestions = 3;

    /// <summary>The largest edit distance at which a name that is not a prefix match is suggested.</summary>
    private const int MaxSuggestionEditDistance = 2;

    /// <summary>Every platform bit that <see cref="CommandPlatforms"/> defines. A command that holds any other bit is refused.</summary>
    private static readonly CommandPlatforms DefinedPlatforms =
        Enum.GetValues<CommandPlatforms>().Aggregate(CommandPlatforms.None, (all, platform) => all | platform);

    /// <summary>
    /// A parser that runs only the slash-command recognizer. Typed input is split by the same recognizer the prompt parser uses,
    /// and any text the recognizer does not claim yields no command.
    /// </summary>
    private static readonly PromptParser SlashParser = new(new IPromptRecognizer[] { new SlashCommandRecognizer() });

    private readonly ReadOnlyCollection<SlashCommandDescriptor> _commands;
    private readonly Dictionary<string, SlashCommandDescriptor> _byName;

    /// <summary>Creates a registry over the given descriptors.</summary>
    /// <param name="descriptors">The commands to register. Their order does not matter; the registry lists them by name.</param>
    /// <exception cref="ArgumentNullException">A descriptor, or a required collection on one, is null.</exception>
    /// <exception cref="ArgumentException">
    /// A name is not one the slash-command recognizer accepts, two commands share a name, a handler id, a description or a disabled
    /// reason is blank, a platform value holds a bit that <see cref="CommandPlatforms"/> does not define, or two arguments of one
    /// command share a name.
    /// </exception>
    public SlashCommandRegistry(IEnumerable<SlashCommandDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        _byName = new Dictionary<string, SlashCommandDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptor, nameof(descriptors));
            Validate(descriptor);
            // The registry stores a copy of the descriptor with its own argument array, so a caller that later changes the list it
            // passed cannot change what the registry reports.
            var stored = descriptor with { Arguments = descriptor.Arguments.ToArray() };
            if (!_byName.TryAdd(stored.Name, stored))
            {
                throw new ArgumentException($"Slash command '{descriptor.Name}' is registered more than once.", nameof(descriptors));
            }
        }

        _commands = Array.AsReadOnly(_byName.Values.OrderBy(static descriptor => descriptor.Name, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Lists every registered command in ordinal name order.</summary>
    public IReadOnlyList<SlashCommandDescriptor> ListAll() => _commands;

    /// <summary>Lists the commands whose names start with the prefix, in ordinal name order. The empty prefix matches every command.</summary>
    /// <param name="prefix">The name prefix. Matching is case-sensitive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is null.</exception>
    public IReadOnlyList<SlashCommandDescriptor> MatchByPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return _commands.Where(descriptor => descriptor.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    }

    /// <summary>
    /// Suggests up to <see cref="MaxSuggestions"/> registered names close to an unknown name. A name that starts with the input
    /// qualifies at any distance; any other name qualifies only within <see cref="MaxSuggestionEditDistance"/> edits. Prefix matches
    /// come first, then the nearer name, then ordinal name order. The result is empty when the catalogue is empty.
    /// </summary>
    /// <param name="unknownName">The name the user typed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="unknownName"/> is null.</exception>
    public IReadOnlyList<string> Suggest(string unknownName)
    {
        ArgumentNullException.ThrowIfNull(unknownName);
        return _commands
            .Select(descriptor => Candidate(unknownName, descriptor.Name))
            .Where(candidate => candidate.IsPrefix || candidate.Distance <= MaxSuggestionEditDistance)
            .OrderByDescending(candidate => candidate.IsPrefix)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .Take(MaxSuggestions)
            .Select(candidate => candidate.Name)
            .ToArray();
    }

    /// <summary>Resolves a command name to available, disabled, or unknown.</summary>
    /// <param name="name">The command name without the leading slash. Matching is case-sensitive.</param>
    /// <returns>
    /// <see cref="SlashCommandAvailable"/> for an enabled command, <see cref="SlashCommandDisabled"/> with the stored reason for a
    /// disabled one, and <see cref="SlashCommandUnknown"/> with suggestions for any other name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public SlashCommandResolution Resolve(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_byName.TryGetValue(name, out var descriptor))
        {
            return new SlashCommandUnknown(name, Suggest(name));
        }

        return descriptor.DisabledReason is { } reason
            ? new SlashCommandDisabled(descriptor, reason)
            : new SlashCommandAvailable(descriptor);
    }

    /// <summary>Splits typed input into a command name and its argument text, through the slash-command recognizer.</summary>
    /// <param name="input">The typed input.</param>
    /// <returns>
    /// The invocation, or <see langword="null"/> when the recognizer does not claim the input: it does not start with a slash
    /// command, or the command is inside a code fence.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public static SlashCommandInvocation? Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Recognize(input);
    }

    private static SlashCommandInvocation? Recognize(string text)
    {
        var envelope = SlashParser.Parse(text);
        return envelope.Kind == IntentKind.SlashCommand
            ? new SlashCommandInvocation(envelope.Arguments["command"], envelope.Arguments["args"])
            : null;
    }

    private static void Validate(SlashCommandDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Name, nameof(descriptor));

        // A name is valid only when the recognizer reads "/name" back as exactly that name with no argument text.
        if (!string.Equals(Recognize("/" + descriptor.Name)?.Name, descriptor.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Slash command name '{descriptor.Name}' is not a name the slash-command recognizer accepts.", nameof(descriptor));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.HandlerId, nameof(descriptor));
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Description, nameof(descriptor));
        if (descriptor.DisabledReason is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.DisabledReason, nameof(descriptor));
        }

        if ((descriptor.Platforms & ~DefinedPlatforms) != CommandPlatforms.None)
        {
            throw new ArgumentException(
                $"Slash command '{descriptor.Name}' has a platform bit that CommandPlatforms does not define.", nameof(descriptor));
        }

        ArgumentNullException.ThrowIfNull(descriptor.Arguments, nameof(descriptor));
        var argumentNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var argument in descriptor.Arguments)
        {
            ArgumentNullException.ThrowIfNull(argument, nameof(descriptor));
            ArgumentException.ThrowIfNullOrWhiteSpace(argument.Name, nameof(descriptor));
            if (!argumentNames.Add(argument.Name))
            {
                throw new ArgumentException(
                    $"Slash command '{descriptor.Name}' declares argument '{argument.Name}' more than once.", nameof(descriptor));
            }
        }
    }

    /// <summary>
    /// Pairs a registered name with its distance from the unknown name. A prefix match is exactly the number of characters the
    /// name has beyond the input, since appending them is the only edit needed, so the full edit distance is computed only for a
    /// name that is not a prefix match.
    /// </summary>
    private static (string Name, bool IsPrefix, int Distance) Candidate(string unknownName, string name) =>
        name.StartsWith(unknownName, StringComparison.Ordinal)
            ? (name, true, name.Length - unknownName.Length)
            : (name, false, EditDistance(unknownName, name));

    private static int EditDistance(string source, string target)
    {
        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (var column = 0; column <= target.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= source.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= target.Length; column++)
            {
                var substitutionCost = source[row - 1] == target[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }
}
