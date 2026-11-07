using Autonogrammer.Contracts.Cowork;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>A Cowork catalog the shell tests fill by hand. It is not a Code catalog.</summary>
internal sealed class MemoryCoworkCatalog : ICoworkSessionCatalog
{
    private readonly List<CoworkSessionSummary> _rows = [];

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Adds a row and raises <see cref="Changed"/>.</summary>
    /// <param name="row">The row.</param>
    public void Add(CoworkSessionSummary row)
    {
        _rows.Insert(0, row);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public IReadOnlyList<CoworkSessionSummary> Snapshot() => [.. _rows];
}
