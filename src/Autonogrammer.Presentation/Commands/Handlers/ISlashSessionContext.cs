namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>What the copy commands read about the chat in front. The head implements it over its session view model.</summary>
public interface ISlashSessionContext
{
    /// <summary>Gets the session id, or <see langword="null"/> before the session has one.</summary>
    string? SessionId { get; }

    /// <summary>Gets the id of the last request sent to the engine (the ACP prompt request or turn id), or <see langword="null"/> before any was sent.</summary>
    string? LastRequestId { get; }

    /// <summary>Gets the text of the assistant's last response, or <see langword="null"/> when there is none.</summary>
    string? LastResponseText { get; }

    /// <summary>Gets the text of the last message the user sent, or <see langword="null"/> when there is none.</summary>
    string? LastUserMessageText { get; }
}
