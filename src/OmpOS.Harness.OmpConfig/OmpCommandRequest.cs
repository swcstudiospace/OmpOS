namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// One omp invocation: the arguments after the executable name, optional bytes for standard input, and whether the
/// standard output is kept. Arguments are passed one by one and never through a shell.
/// </summary>
public sealed class OmpCommandRequest
{
    /// <summary>Creates a request.</summary>
    /// <param name="arguments">The arguments after the executable name. They must not carry a secret: use <see cref="StandardInput"/> for that.</param>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is null.</exception>
    public OmpCommandRequest(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Arguments = arguments;
    }

    /// <summary>Gets the arguments after the executable name.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the bytes written to the process's standard input, which is then closed. Null writes nothing and closes the input
    /// at once. The caller owns the memory and may clear it after the invocation returns.
    /// </summary>
    public ReadOnlyMemory<byte>? StandardInput { get; init; }

    /// <summary>
    /// Gets a value indicating whether the standard output is kept in the result. False reads and discards it, for a command
    /// whose output is itself a secret. The default is true.
    /// </summary>
    public bool CaptureStandardOutput { get; init; } = true;

    /// <summary>Returns the arguments only. Standard input is never rendered.</summary>
    /// <returns>A short description.</returns>
    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"omp {string.Join(' ', Arguments)}");
}
