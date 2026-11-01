namespace Autonogrammer.Presentation.Cowork;

/// <summary>The composer surface bound by the native composer view. It is the conversation, named as its own type so mapping can tell the views apart.</summary>
public sealed class CoworkComposerModel
{
    /// <summary>Creates the surface.</summary>
    /// <param name="conversation">The conversation that owns the composer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="conversation"/> is null.</exception>
    public CoworkComposerModel(CoworkConversationModel conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        Conversation = conversation;
    }

    /// <summary>Gets the conversation.</summary>
    public CoworkConversationModel Conversation { get; }
}
