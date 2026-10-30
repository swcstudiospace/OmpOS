namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// A content contract for one dock panel. It is not a slot: it has no position. The head maps <see cref="Content"/> to a control.
/// </summary>
public interface IDockPanel
{
    /// <summary>Gets the stable panel identifier.</summary>
    string Id { get; }

    /// <summary>Gets the tab title.</summary>
    string Title { get; }

    /// <summary>Gets the presentation view model the head maps to a control.</summary>
    object Content { get; }
}
