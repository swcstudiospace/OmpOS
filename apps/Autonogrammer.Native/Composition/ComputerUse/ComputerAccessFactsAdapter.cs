using Autonogrammer.Cowork;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>
/// Account facts over <see cref="ICoworkAccountGate"/> only. Signed out is not a Cowork session.
/// This type does not read an account subject.
/// </summary>
internal sealed class ComputerAccessFactsAdapter : IComputerUseAccessFacts
{
    private readonly ICoworkAccountGate _gate;

    /// <summary>Creates the adapter.</summary>
    /// <param name="gate">The Cowork account gate. The adapter reads <see cref="ICoworkAccountGate.IsSignedIn"/> only.</param>
    /// <exception cref="ArgumentNullException"><paramref name="gate"/> is null.</exception>
    public ComputerAccessFactsAdapter(ICoworkAccountGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        _gate = gate;
    }

    /// <inheritdoc />
    public bool IsSignedIn => _gate.IsSignedIn;

    /// <inheritdoc />
    public bool IsCoworkSession(string sessionId) => _gate.IsSignedIn && !string.IsNullOrWhiteSpace(sessionId);
}
