using System.Globalization;

namespace OmpOS.Presentation.Queue;

/// <summary>The words of the message queue.</summary>
public static class QueueCopy
{
    /// <summary>The title of the queue panel and of the Queued pill.</summary>
    public const string Title = "Queued";

    /// <summary>The sentence shown while the queue is empty.</summary>
    public const string Empty = "Nothing is queued. A message you send while the agent works waits here and runs after its turn.";

    /// <summary>The label of the control that sends a queued message into the running turn.</summary>
    public const string SendNowLabel = "Send now";

    /// <summary>The label of the control that removes a queued message.</summary>
    public const string RemoveLabel = "Remove";

    /// <summary>The label of the control that empties the queue.</summary>
    public const string ClearLabel = "Clear queue";

    /// <summary>The announcement when the queue is emptied.</summary>
    public const string Cleared = "Queue cleared.";

    /// <summary>Gets the sentence for the number of queued messages.</summary>
    /// <param name="count">The number of messages.</param>
    /// <returns>The sentence.</returns>
    public static string Count(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} queued");

    /// <summary>Gets the sentence for a full queue.</summary>
    /// <param name="max">The most messages the queue holds.</param>
    /// <returns>The sentence.</returns>
    public static string Full(int max) => string.Create(CultureInfo.InvariantCulture, $"The queue is full ({max} messages). Wait for the agent, or remove one.");

    /// <summary>Gets the accessible name of a row.</summary>
    /// <param name="position">The message's place, from 1.</param>
    /// <param name="preview">The message as one line.</param>
    /// <returns>The name.</returns>
    public static string RowName(int position, string preview) => string.Create(CultureInfo.InvariantCulture, $"Queued message {position}: {preview}");

    /// <summary>Gets the accessible name of a Remove control.</summary>
    /// <param name="position">The message's place.</param>
    /// <returns>The name.</returns>
    public static string RemoveName(int position) => string.Create(CultureInfo.InvariantCulture, $"Remove queued message {position}");

    /// <summary>Gets the accessible name of a Move up control.</summary>
    /// <param name="position">The message's place.</param>
    /// <returns>The name.</returns>
    public static string MoveUpName(int position) => string.Create(CultureInfo.InvariantCulture, $"Move queued message {position} up");

    /// <summary>Gets the accessible name of a Move down control.</summary>
    /// <param name="position">The message's place.</param>
    /// <returns>The name.</returns>
    public static string MoveDownName(int position) => string.Create(CultureInfo.InvariantCulture, $"Move queued message {position} down");

    /// <summary>Gets the accessible name of a Send now control.</summary>
    /// <param name="position">The message's place.</param>
    /// <returns>The name.</returns>
    public static string SendNowName(int position) => string.Create(CultureInfo.InvariantCulture, $"Send queued message {position} now");

    /// <summary>Gets the announcement for a message that moved.</summary>
    /// <param name="from">Where it was.</param>
    /// <param name="to">Where it is.</param>
    /// <param name="count">How many messages are queued.</param>
    /// <returns>The announcement.</returns>
    public static string Moved(int from, int to, int count) => string.Create(CultureInfo.InvariantCulture, $"Moved message {from} to position {to} of {count}.");

    /// <summary>Gets the announcement for a message that was sent now.</summary>
    /// <param name="position">The message's place before it left.</param>
    /// <returns>The announcement.</returns>
    public static string SentNow(int position) => string.Create(CultureInfo.InvariantCulture, $"Sent queued message {position} to the agent now.");

    /// <summary>Gets the announcement for a message that could not be sent now.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>The announcement.</returns>
    public static string SendFailed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return "Could not send it now: " + reason.TrimEnd('.') + ". It stays in the queue.";
    }

    /// <summary>The announcement when no turn is running any more.</summary>
    public const string NoRun = "The agent is not working any more. The message stays queued and runs next.";
}
