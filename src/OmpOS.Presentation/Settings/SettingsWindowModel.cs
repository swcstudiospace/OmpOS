using System.ComponentModel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// The settings window state: its pages in navigation order, the selected page and the close protocol. Closing with unsaved
/// changes does not close; it shows a prompt that the view answers through <see cref="ResolveClosePromptAsync"/>, which saves
/// every dirty page when the answer is Save.
/// </summary>
public sealed class SettingsWindowModel : ObservableModel
{
    private readonly ISettingsPage[] _pages;
    private SettingsSection _selectedSection;
    private bool _isClosePromptVisible;
    private bool _isClosed;

    /// <summary>Initializes a new instance of the <see cref="SettingsWindowModel"/> class.</summary>
    /// <param name="pages">The pages in navigation order. Non-empty, with a distinct section each. The first page is selected.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pages"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="pages"/> is empty, holds a null page or repeats a section.</exception>
    public SettingsWindowModel(IReadOnlyList<ISettingsPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        _pages = [.. pages];
        if (_pages.Length == 0)
        {
            throw new ArgumentException("The settings window needs at least one page.", nameof(pages));
        }

        var sections = new HashSet<SettingsSection>();
        foreach (var page in _pages)
        {
            if (page is null)
            {
                throw new ArgumentException("A settings page is null.", nameof(pages));
            }

            if (!sections.Add(page.Section))
            {
                throw new ArgumentException("Two pages fill the same settings section.", nameof(pages));
            }

            page.PropertyChanged += OnPagePropertyChanged;
        }

        _selectedSection = _pages[0].Section;
    }

    /// <summary>Gets the pages in navigation order.</summary>
    public IReadOnlyList<ISettingsPage> Pages => _pages;

    /// <summary>Gets the selected page.</summary>
    public ISettingsPage SelectedPage => Find(_selectedSection);

    /// <summary>Gets the selected section.</summary>
    public SettingsSection SelectedSection => _selectedSection;

    /// <summary>Gets a value indicating whether the unsaved-changes prompt is showing.</summary>
    public bool IsClosePromptVisible => _isClosePromptVisible;

    /// <summary>Gets a value indicating whether the window has closed.</summary>
    public bool IsClosed => _isClosed;

    /// <summary>Gets a value indicating whether any page has unsaved changes.</summary>
    public bool HasUnsavedChanges => _pages.Any(page => page.IsDirty);

    /// <summary>Loads every page. A page that cannot load shows its own error state; the others are unaffected.</summary>
    /// <param name="cancellationToken">Cancels the loads.</param>
    /// <returns>A task that completes when every page is ready or has failed to load.</returns>
    public async Task LoadAllAsync(CancellationToken cancellationToken)
    {
        // The continuation must run on the context that owns the model, so the context is captured.
        await Task.WhenAll(_pages.Select(page => page.LoadAsync(cancellationToken))).ConfigureAwait(true);
        NotifyPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Selects a section.</summary>
    /// <param name="section">The section to show.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="section"/> is not a section of this window.</exception>
    public void SelectSection(SettingsSection section)
    {
        _ = Find(section);
        if (SetProperty(ref _selectedSection, section, nameof(SelectedSection)))
        {
            NotifyPropertyChanged(nameof(SelectedPage));
        }
    }

    /// <summary>Saves one page.</summary>
    /// <param name="section">The section to save.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>True when the page was saved or had nothing to save; false when an entry is rejected (the inline errors show it).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="section"/> is not a section of this window.</exception>
    public async Task<bool> SaveAsync(SettingsSection section, CancellationToken cancellationToken)
    {
        var page = Find(section);

        // The continuation must run on the context that owns the model, so the context is captured.
        var saved = await page.SaveAsync(cancellationToken).ConfigureAwait(true);
        NotifyPropertyChanged(nameof(HasUnsavedChanges));
        return saved;
    }

    /// <summary>Resets one page's draft to its defaults. The saved values are unchanged until the page is saved.</summary>
    /// <param name="section">The section to reset.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="section"/> is not a section of this window.</exception>
    public void ResetSection(SettingsSection section)
    {
        Find(section).Reset();
        NotifyPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>Asks the window to close. With unsaved changes it stays open and shows the prompt first.</summary>
    /// <returns>Whether the window closed or the prompt is waiting for an answer.</returns>
    public SettingsCloseOutcome RequestClose()
    {
        if (_isClosed)
        {
            return SettingsCloseOutcome.Closed;
        }

        if (HasUnsavedChanges)
        {
            SetProperty(ref _isClosePromptVisible, true, nameof(IsClosePromptVisible));
            return SettingsCloseOutcome.ConfirmationRequired;
        }

        SetProperty(ref _isClosed, true, nameof(IsClosed));
        return SettingsCloseOutcome.Closed;
    }

    /// <summary>
    /// Answers the unsaved-changes prompt. Save saves every dirty page; a page that rejects its save keeps the window open with that
    /// page selected and its inline error showing, while the other dirty pages are still saved.
    /// </summary>
    /// <param name="choice">The user's choice.</param>
    /// <param name="cancellationToken">Cancels the saves.</param>
    /// <returns>True when the window closed.</returns>
    /// <exception cref="InvalidOperationException">The prompt is not showing.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="choice"/> is not a defined choice.</exception>
    public Task<bool> ResolveClosePromptAsync(SettingsClosePromptChoice choice, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(choice))
        {
            throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unknown close prompt choice.");
        }

        if (!_isClosePromptVisible)
        {
            throw new InvalidOperationException("The unsaved-changes prompt is not showing.");
        }

        SetProperty(ref _isClosePromptVisible, false, nameof(IsClosePromptVisible));
        return choice switch
        {
            SettingsClosePromptChoice.KeepEditing => Task.FromResult(false),
            SettingsClosePromptChoice.Discard => Task.FromResult(DiscardAllAndClose()),
            _ => SaveAllAndCloseAsync(cancellationToken),
        };
    }

    private async Task<bool> SaveAllAndCloseAsync(CancellationToken cancellationToken)
    {
        ISettingsPage? rejected = null;
        foreach (var page in _pages.Where(page => page.IsDirty))
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            if (!await page.SaveAsync(cancellationToken).ConfigureAwait(true))
            {
                rejected ??= page;
            }
        }

        NotifyPropertyChanged(nameof(HasUnsavedChanges));
        if (rejected is not null)
        {
            SelectSection(rejected.Section);
            return false;
        }

        SetProperty(ref _isClosed, true, nameof(IsClosed));
        return true;
    }

    private bool DiscardAllAndClose()
    {
        foreach (var page in _pages)
        {
            page.Discard();
        }

        NotifyPropertyChanged(nameof(HasUnsavedChanges));
        SetProperty(ref _isClosed, true, nameof(IsClosed));
        return true;
    }

    private ISettingsPage Find(SettingsSection section)
    {
        foreach (var page in _pages)
        {
            if (page.Section == section)
            {
                return page;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(section), section, "The settings window has no page for this section.");
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ISettingsPage.IsDirty), StringComparison.Ordinal))
        {
            NotifyPropertyChanged(nameof(HasUnsavedChanges));
        }
    }
}
