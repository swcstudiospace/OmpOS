using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>Reads the reduced-motion setting of a control from the <c>reduced-motion</c> class that the window and the main view carry.</summary>
internal static class MotionProbe
{
    /// <summary>The class the window and the main view carry while motion is reduced.</summary>
    internal const string ReducedMotionClass = "reduced-motion";

    /// <summary>Returns whether the control or any ancestor carries the reduced-motion class.</summary>
    /// <param name="element">The control to test.</param>
    /// <returns>True when motion is reduced for the control.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> is null.</exception>
    internal static bool IsReduced(StyledElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (element.Classes.Contains(ReducedMotionClass))
        {
            return true;
        }

        if (element.GetLogicalAncestors().OfType<StyledElement>().Any(static ancestor => ancestor.Classes.Contains(ReducedMotionClass)))
        {
            return true;
        }

        return element is Visual visual
            && visual.GetVisualAncestors().OfType<StyledElement>().Any(static ancestor => ancestor.Classes.Contains(ReducedMotionClass));
    }
}
