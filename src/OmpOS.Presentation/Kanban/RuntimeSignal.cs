namespace OmpOS.Presentation.Kanban;

/// <summary>
/// A runtime event that may set a card's status. The base carries only the signal kind; concrete signal records, which carry the
/// data their runtime contract provides, derive from it and cannot be created from the base.
/// </summary>
/// <param name="Kind">The runtime fact this signal reports.</param>
public abstract record RuntimeSignal(RuntimeSignalKind Kind);
