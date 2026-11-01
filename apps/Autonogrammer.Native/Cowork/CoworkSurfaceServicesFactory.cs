using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Views.Shell;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Supplies the conversation delegate to the phase-69 builder. It does not build a second <see cref="CoworkSurfaceServices"/>.
/// </summary>
internal static class CoworkSurfaceServicesFactory
{
    /// <summary>Builds the window services and adds <see cref="CoworkSurfaceServices.OpenConversation"/>.</summary>
    /// <param name="head">The composed head.</param>
    /// <returns>The services, or null when Cowork is not available.</returns>
    public static CoworkSurfaceServices? TryBuild(AgentHead head)
    {
        var built = CoworkSessionCatalogAdapter.TryBuild(head);
        if (built is null)
        {
            return null;
        }

        return built with
        {
            OpenConversation = sessionId => head.Cowork?.Sessions.ConversationOf(sessionId),
        };
    }
}
