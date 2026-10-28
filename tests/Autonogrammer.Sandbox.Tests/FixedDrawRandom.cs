namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Random source that returns one fixed draw in [0, 1), so jitter bounds can be asserted exactly.
/// </summary>
internal sealed class FixedDrawRandom : Random
{
    private readonly double _draw;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedDrawRandom"/> class.
    /// </summary>
    /// <param name="draw">The value returned by every <see cref="NextDouble"/> call.</param>
    public FixedDrawRandom(double draw)
    {
        _draw = draw;
    }

    /// <inheritdoc />
    public override double NextDouble() => _draw;
}
