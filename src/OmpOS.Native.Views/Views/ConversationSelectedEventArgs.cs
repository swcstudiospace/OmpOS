namespace OmpOS.Native.Views;

/// <summary>The conversation the user chose in the session list.</summary>
internal sealed class ConversationSelectedEventArgs(string id) : EventArgs
{
    /// <summary>Gets the identifier of the chosen conversation.</summary>
    public string Id { get; } = id ?? throw new ArgumentNullException(nameof(id));
}
