namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>The Changes bar of the chat in front.</summary>
public interface IChangesBarOpener
{
    /// <summary>Gets whether the session changed any file. The bar is hidden while it is false.</summary>
    bool HasChanges { get; }

    /// <summary>Shows and focuses the Changes bar.</summary>
    void Show();
}
