namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Element refs from one accessibility snapshot. A mismatched generation is stale.</summary>
public sealed class ElementRefTable
{
    private readonly Dictionary<string, ResolvedRef> _refs = new(StringComparer.Ordinal);
    private int _generation;

    /// <summary>Gets the generation of the current snapshot.</summary>
    public int Generation => _generation;

    /// <summary>The failure text for a stale ref.</summary>
    public const string StaleRef = "StaleRef";

    /// <summary>Replaces the table from accessibility nodes that carry a backend node id.</summary>
    /// <param name="nodes">The nodes.</param>
    public void Replace(IReadOnlyList<AxRefNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        _refs.Clear();
        _generation++;
        var index = 1;
        foreach (var node in nodes)
        {
            _refs["e" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new ResolvedRef(node.BackendDomNodeId, node.Role, node.Name, node.X, node.Y);
            index++;
        }
    }

    /// <summary>Resolves a ref against a snapshot generation.</summary>
    /// <param name="refId">The ref, such as e1.</param>
    /// <param name="generation">The generation the caller observed.</param>
    /// <param name="resolved">The node, when fresh.</param>
    /// <returns>False when the ref is stale or unknown.</returns>
    public bool TryResolve(string refId, int generation, out ResolvedRef resolved)
    {
        if (generation != _generation || string.IsNullOrEmpty(refId) || !_refs.TryGetValue(refId, out resolved))
        {
            resolved = default;
            return false;
        }

        return true;
    }

    /// <summary>Scales screenshot coordinates back to viewport CSS pixels.</summary>
    /// <param name="x">The screenshot x.</param>
    /// <param name="y">The screenshot y.</param>
    /// <param name="frameWidth">The returned frame width.</param>
    /// <param name="frameHeight">The returned frame height.</param>
    /// <param name="viewportWidth">The viewport width.</param>
    /// <param name="viewportHeight">The viewport height.</param>
    /// <returns>The CSS-pixel point.</returns>
    public static (double X, double Y) Scale(double x, double y, double frameWidth, double frameHeight, double viewportWidth, double viewportHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return (x, y);
        }

        return (x * viewportWidth / frameWidth, y * viewportHeight / frameHeight);
    }
}
