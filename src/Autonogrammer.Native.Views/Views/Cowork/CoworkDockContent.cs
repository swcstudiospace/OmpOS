namespace Autonogrammer.Native.Views.Cowork;

/// <summary>
/// The presentation object a dock panel carries when it has no richer view model. <c>ViewMapping</c> maps it to a named block.
/// </summary>
public sealed class CoworkDockContent
{
    /// <summary>Creates the marker.</summary>
    /// <param name="label">The text the mapped view shows.</param>
    /// <exception cref="ArgumentException"><paramref name="label"/> is empty.</exception>
    public CoworkDockContent(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label;
    }

    /// <summary>Gets the text the mapped view shows.</summary>
    public string Label { get; }
}
