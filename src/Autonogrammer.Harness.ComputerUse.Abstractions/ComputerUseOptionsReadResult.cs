namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Options plus key-only failures from <see cref="ComputerUseOptionsReader"/>.</summary>
public sealed class ComputerUseOptionsReadResult
{
    /// <summary>Creates a result.</summary>
    /// <param name="options">The options, with defaults kept where a variable was absent or unparsable.</param>
    /// <param name="failures">Key-only failures. Values are never copied into these strings.</param>
    public ComputerUseOptionsReadResult(ComputerUseOptions options, IReadOnlyList<string> failures)
    {
        Options = options;
        Failures = failures;
    }

    /// <summary>The options.</summary>
    public ComputerUseOptions Options { get; }

    /// <summary>Parse and validation failures. Each string names a key and never a value.</summary>
    public IReadOnlyList<string> Failures { get; }
}

