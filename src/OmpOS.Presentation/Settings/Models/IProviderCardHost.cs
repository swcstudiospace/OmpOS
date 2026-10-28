namespace OmpOS.Presentation.Settings.Models;

/// <summary>What a provider card needs from the page that owns it.</summary>
internal interface IProviderCardHost
{
    /// <summary>Reads the list again after a change omp has accepted, and announces the change.</summary>
    /// <param name="announcement">The text for the live region.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is current.</returns>
    Task ChangedAsync(string announcement, CancellationToken cancellationToken);

    /// <summary>Records that a model is now the default.</summary>
    /// <param name="selector">The new default.</param>
    void DefaultChanged(string selector);

    /// <summary>Opens the custom provider form for an OmpOS managed provider.</summary>
    /// <param name="card">The card whose provider is to be edited.</param>
    void EditRequested(ProviderCardModel card);

    /// <summary>Puts text in the live region.</summary>
    /// <param name="text">The text.</param>
    void Announce(string text);

    /// <summary>Gets the sentence that says why the operator has disabled key entry, or null when keys can be entered.</summary>
    string? KeyWritesDisabledReason { get; }
}
