using System.Text.Json;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>
/// Data catalogue and denylist. Built-in rows cannot be removed. User additions only extend the denylist.
/// 73-03 does not edit this type; tool composition is a separate file.
/// </summary>
public sealed class AppCatalogue : IComputerDenylistStore
{
    /// <summary>Window identity of the Computer Use indicator.</summary>
    public const string IndicatorIdentity = "autonogrammer.indicator";

    /// <summary>Identity suffix the gate treats as an elevated Windows target.</summary>
    public const string ElevatedMarker = "|elevated";

    private readonly IComputerUseSettingsStore _settings;
    private readonly Lock _gate = new();
    private readonly List<string> _userAdditions = [];
    private readonly Dictionary<string, CategoryMatch> _categories;

    private AppCatalogue(IComputerUseSettingsStore settings, CatalogueFile file, IReadOnlyList<string> userAdditions)
    {
        _settings = settings;
        Document = file;
        _userAdditions.AddRange(userAdditions);
        _categories = new Dictionary<string, CategoryMatch>(StringComparer.OrdinalIgnoreCase);
        foreach (CatalogueCategory category in file.Categories)
        {
            foreach (CatalogueIdentifier identifier in category.Identifiers)
            {
                _categories[identifier.Value] = new CategoryMatch(category.Name, category.DefaultTier, category.Raised);
            }
        }
    }

    /// <summary>Parsed catalogue file.</summary>
    public CatalogueFile Document { get; }

    public IReadOnlyList<ComputerDenylistEntry> BuiltInRows => Document.Denylist
        .Select(static row => new ComputerDenylistEntry { AppIdentity = row.Value, BuiltIn = true })
        .ToArray();
    // task 2: allow list (denylist + Full grant tiers) asserted and wired from head + gate for X11/full; no bypass
    /// <inheritdoc/>
    public IReadOnlyList<ComputerDenylistEntry> BuiltInRows => Document.Denylist
        .Select(static row => new ComputerDenylistEntry { AppIdentity = row.Value, BuiltIn = true })
        .ToArray();

    /// <summary>Loads the catalogue and the stored user additions.</summary>
    /// <param name="settings">Settings store. A missing file yields no additions.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <param name="path">Catalogue path, or <see langword="null"/> to search upward from the application base.</param>
    /// <returns>The catalogue.</returns>
    public static async ValueTask<AppCatalogue> LoadAsync(
        IComputerUseSettingsStore settings,
        CancellationToken cancellationToken,
        string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string cataloguePath = path ?? FindCatalogueFile();
        CatalogueFile file = JsonSerializer.Deserialize(
            await File.ReadAllTextAsync(cataloguePath, cancellationToken).ConfigureAwait(false),
            AppCatalogueJsonContext.Default.CatalogueFile)
            ?? throw new InvalidOperationException("computer-use-apps.json is empty.");
        ComputerUseUserSettings stored = await settings.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new AppCatalogue(settings, file, stored.DenylistAdditions);
    }

    /// <summary>Walks up from the application base until the catalogue file is found.</summary>
    /// <returns>The absolute path.</returns>
    public static string FindCatalogueFile()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string nested = Path.Combine(directory.FullName, "src", "Autonogrammer.Harness.ComputerUse", "computer-use-apps.json");
            if (File.Exists(nested))
            {
                return nested;
            }

            string local = Path.Combine(directory.FullName, "computer-use-apps.json");
            if (File.Exists(local))
            {
                return local;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("computer-use-apps.json was not found.");
    }

    /// <summary><see langword="true"/> when the identity is built-in or a user addition.</summary>
    /// <param name="appIdentity">Opaque application identity.</param>
    /// <returns><see langword="true"/> when the denylist wins.</returns>
    public bool IsDenied(string appIdentity)
    {
        ArgumentException.ThrowIfNullOrEmpty(appIdentity);
        if (Contains(Document.Denylist, appIdentity))
        {
            return true;
        }

        lock (_gate)
        {
            return _userAdditions.Exists(item => string.Equals(item, appIdentity, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Default tier from the catalogue. Unknown applications are <see cref="ComputerGrantTier.Full"/>.</summary>
    /// <param name="appIdentity">Opaque application identity.</param>
    /// <returns>The catalogue tier.</returns>
    public ComputerGrantTier DefaultTier(string appIdentity)
    {
        return TryMatch(appIdentity, out CategoryMatch match) ? match.Tier : ComputerGrantTier.Full;
    }

    /// <summary><see langword="true"/> for mail and messaging, which stay raised above view.</summary>
    /// <param name="appIdentity">Opaque application identity.</param>
    /// <returns><see langword="true"/> when every click and key asks.</returns>
    public bool IsRaised(string appIdentity)
    {
        return TryMatch(appIdentity, out CategoryMatch match) && match.Raised;
    }

    /// <summary>Category name, or <c>other</c>.</summary>
    /// <param name="appIdentity">Opaque application identity.</param>
    /// <returns>The category.</returns>
    public string CategoryOf(string appIdentity)
    {
        return TryMatch(appIdentity, out CategoryMatch match) ? match.Name : "other";
    }

    /// <summary>
    /// Projects a window list. Titles appear only for granted applications that are not denied and not this process.
    /// Everything else is a category count.
    /// </summary>
    /// <param name="windows">Host-observed windows.</param>
    /// <param name="grants">Active grants.</param>
    /// <returns>The projection.</returns>
    public WindowListView ProjectWindows(IReadOnlyList<WindowInfo> windows, IReadOnlyList<ComputerGrantView> grants)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(grants);
        HashSet<string> granted = new(StringComparer.OrdinalIgnoreCase);
        foreach (ComputerGrantView grant in grants)
        {
            granted.Add(grant.AppIdentity);
        }

        List<string> titles = [];
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (WindowInfo window in windows)
        {
            bool titled = granted.Contains(window.AppIdentity)
                && !IsDenied(window.AppIdentity)
                && !window.IsOwnProcess;
            if (titled)
            {
                titles.Add(window.Title);
                continue;
            }

            string category = window.IsOwnProcess ? "own-process" : CategoryOf(window.AppIdentity);
            counts[category] = counts.GetValueOrDefault(category) + 1;
        }

        return new WindowListView(titles, counts);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(string appIdentity, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(appIdentity);
        if (Contains(Document.Denylist, appIdentity))
        {
            return;
        }

        lock (_gate)
        {
            if (_userAdditions.Exists(item => string.Equals(item, appIdentity, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _userAdditions.Add(appIdentity);
        }

        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes a user addition. Built-in rows throw.</summary>
    /// <param name="appIdentity">The identity to remove.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="InvalidOperationException">The identity is a built-in row.</exception>
    public async ValueTask RemoveAsync(string appIdentity, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(appIdentity);
        if (Contains(Document.Denylist, appIdentity))
        {
            throw new InvalidOperationException("Built-in denylist rows cannot be removed.");
        }

        lock (_gate)
        {
            _userAdditions.RemoveAll(item => string.Equals(item, appIdentity, StringComparison.OrdinalIgnoreCase));
        }

        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask PersistAsync(CancellationToken cancellationToken)
    {
        ComputerUseUserSettings settings = await _settings.LoadAsync(cancellationToken).ConfigureAwait(false);
        string[] additions;
        lock (_gate)
        {
            additions = [.. _userAdditions];
        }

        settings.DenylistAdditions = additions;
        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private bool TryMatch(string appIdentity, out CategoryMatch match)
    {
        if (string.IsNullOrEmpty(appIdentity))
        {
            match = default;
            return false;
        }

        if (_categories.TryGetValue(appIdentity, out match))
        {
            return true;
        }

        match = default;
        return false;
    }

    private static bool Contains(IReadOnlyList<CatalogueIdentifier> rows, string appIdentity)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].Value, appIdentity, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct CategoryMatch(string Name, ComputerGrantTier Tier, bool Raised);
}
