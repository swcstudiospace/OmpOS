namespace Autonogrammer.Cowork.Abstractions;

/// <summary>The host's session slot. The chat core sets the turn origin. Taint is host state and is never reset.</summary>
public interface ICoworkSessionLease : IAsyncDisposable
{
    /// <summary>Sets the origin of the current turn. Only the chat core calls this.</summary>
    /// <param name="origin">The origin.</param>
    void SetTurnOrigin(CoworkTurnOrigin origin);

    /// <summary>Gets a value indicating whether any result in this session carried untrusted content.</summary>
    bool IsTainted { get; }
}
