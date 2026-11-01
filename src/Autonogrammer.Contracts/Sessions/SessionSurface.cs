namespace Autonogrammer.Contracts.Sessions;

/// <summary>
/// The product surface a session belongs to. The surface is chosen when the session is created and is stored with it, so that
/// <c>ISessionRepository.ListByOwnerAsync</c> can list the sessions of one surface without reading the others.
/// </summary>
public enum SessionSurface
{
    /// <summary>A coding session: an agent working in a workspace with a sandbox. This is the default.</summary>
    Code,

    /// <summary>A Cowork session: a conversation with artifacts, projects and memory that belongs to an account.</summary>
    Cowork,
}
