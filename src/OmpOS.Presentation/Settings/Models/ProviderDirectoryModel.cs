using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// The provider listing of the Models page: every provider, aggregator, coding plan and local runtime the source offers, as cards
/// grouped by region and kind, filtered by a region choice, a kind choice and a search text. The choices carry how many listings each
/// keeps. A filter never changes a card; it only hides it, so typed values and open forms survive a filter change.
/// </summary>
public sealed class ProviderDirectoryModel : ObservableModel
{
    private static readonly ProviderListingKind[] KindOrder =
    [
        ProviderListingKind.Api,
        ProviderListingKind.Aggregator,
        ProviderListingKind.CodingPlan,
        ProviderListingKind.Local,
    ];

    private readonly IModelsSettingsSource _source;
    private readonly IProviderCardHost _host;
    private readonly Dictionary<string, ProviderListingCardModel> _byId = new(StringComparer.Ordinal);
    private List<ProviderListingCardModel> _cards = [];
    private List<ProviderListingRegion> _regions = [];
    private ProviderListingRegion? _region;
    private ProviderListingKind? _kind;
    private string _searchText = string.Empty;
    private List<ProviderListingSection> _sections = [];
    private IReadOnlyList<string> _regionChoices = [ProviderListingCopy.AllChoice];
    private IReadOnlyList<string> _kindChoices = [ProviderListingCopy.AllChoice];
    private string _announcement = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="ProviderDirectoryModel"/> class.</summary>
    /// <param name="source">The source every card action goes through.</param>
    /// <param name="host">The page that owns the directory.</param>
    internal ProviderDirectoryModel(IModelsSettingsSource source, IProviderCardHost host)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(host);
        _source = source;
        _host = host;
    }

    /// <summary>Gets a value indicating whether the source offered any listing.</summary>
    public bool HasListings => _cards.Count > 0;

    /// <summary>Gets every card, whatever the filters keep.</summary>
    public IReadOnlyList<ProviderListingCardModel> Cards => _cards;

    /// <summary>Gets the sections after the filters, ordered by region (Europe, United States, China, global, local) and kind. A section without a card left is absent.</summary>
    public IReadOnlyList<ProviderListingSection> Sections => _sections;

    /// <summary>Gets the number of cards the filters keep.</summary>
    public int VisibleCount => _sections.Sum(section => section.Cards.Count);

    /// <summary>Gets a value indicating whether there are listings and the filters keep none.</summary>
    public bool HasNoMatches => HasListings && _sections.Count == 0;

    /// <summary>Gets the choices of the region filter: all, then each region that has a listing, each with its count.</summary>
    public IReadOnlyList<string> RegionChoices => _regionChoices;

    /// <summary>Gets the choices of the kind filter: all, then each kind, each with its count.</summary>
    public IReadOnlyList<string> KindChoices => _kindChoices;

    /// <summary>Gets or sets the index into <see cref="RegionChoices"/>; zero means all regions.</summary>
    public int SelectedRegionIndex
    {
        get => _region is { } region ? _regions.IndexOf(region) + 1 : 0;
        set
        {
            // A list control reports -1 while it rebuilds its items; that is not a choice.
            if (value < 0)
            {
                return;
            }

            var next = value >= 1 && value <= _regions.Count ? _regions[value - 1] : (ProviderListingRegion?)null;
            SelectRegion(next);
        }
    }

    /// <summary>Gets or sets the index into <see cref="KindChoices"/>; zero means all kinds.</summary>
    public int SelectedKindIndex
    {
        get => _kind is { } kind ? Array.IndexOf(KindOrder, kind) + 1 : 0;
        set
        {
            if (value < 0)
            {
                return;
            }

            var next = value >= 1 && value <= KindOrder.Length ? KindOrder[value - 1] : (ProviderListingKind?)null;
            SelectKind(next);
        }
    }

    /// <summary>Gets the region the filter keeps, or null for all regions.</summary>
    public ProviderListingRegion? SelectedRegion => _region;

    /// <summary>Gets the kind the filter keeps, or null for all kinds.</summary>
    public ProviderListingKind? SelectedKind => _kind;

    /// <summary>Gets or sets the search text. A card stays when its name, id, omp id, country, residency note, endpoint or note contains it.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                Rebuild();
            }
        }
    }

    /// <summary>Gets the text for a live region: how many listings the latest filter change kept.</summary>
    public string Announcement { get => _announcement; private set => SetProperty(ref _announcement, value); }

    /// <summary>Keeps one region, or all regions when <paramref name="region"/> is null.</summary>
    /// <param name="region">The region to keep.</param>
    public void SelectRegion(ProviderListingRegion? region)
    {
        if (region is { } wanted && !_regions.Contains(wanted))
        {
            return;
        }

        if (_region != region)
        {
            _region = region;
            NotifyPropertyChanged(nameof(SelectedRegion));
            NotifyPropertyChanged(nameof(SelectedRegionIndex));
            Rebuild();
            Announcement = ProviderListingCopy.MatchCount(VisibleCount);
        }
    }

    /// <summary>Keeps one kind, or all kinds when <paramref name="kind"/> is null.</summary>
    /// <param name="kind">The kind to keep.</param>
    public void SelectKind(ProviderListingKind? kind)
    {
        if (_kind != kind)
        {
            _kind = kind;
            NotifyPropertyChanged(nameof(SelectedKind));
            NotifyPropertyChanged(nameof(SelectedKindIndex));
            Rebuild();
            Announcement = ProviderListingCopy.MatchCount(VisibleCount);
        }
    }

    /// <summary>Finds a card by listing id.</summary>
    /// <param name="listingId">The listing id.</param>
    /// <returns>The card, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="listingId"/> is null.</exception>
    public ProviderListingCardModel? Find(string listingId)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        return _byId.GetValueOrDefault(listingId);
    }

    /// <summary>Replaces the listings with a newer read and marks the ones omp already has.</summary>
    /// <param name="listings">The listings, in the order the source lists them.</param>
    /// <param name="providers">The provider cards of the page, to find which listings are set up.</param>
    internal void Update(IReadOnlyList<ProviderListing> listings, IReadOnlyList<ProviderCardModel> providers)
    {
        ArgumentNullException.ThrowIfNull(listings);
        ArgumentNullException.ThrowIfNull(providers);
        var cards = new List<ProviderListingCardModel>(listings.Count);
        foreach (var listing in listings)
        {
            if (_byId.TryGetValue(listing.Id, out var card))
            {
                card.Update(listing);
            }
            else
            {
                card = new ProviderListingCardModel(listing, _source, _host);
                _byId[listing.Id] = card;
            }

            cards.Add(card);
        }

        foreach (var staleId in _byId.Keys.Except(cards.Select(card => card.Id), StringComparer.Ordinal).ToArray())
        {
            _ = _byId.Remove(staleId);
        }

        foreach (var card in cards)
        {
            var configured = providers.FirstOrDefault(provider => string.Equals(provider.Id, card.ConfiguredId, StringComparison.Ordinal));
            card.SetConfigured(configured is null ? null : ProviderListingCopy.ConfiguredText(card.TakesKey, configured.Info.Credential == CredentialState.Present));
        }

        _cards = cards;
        _regions = [.. Enum.GetValues<ProviderListingRegion>().Where(region => cards.Exists(card => card.Region == region))];
        if (_region is { } selected && !_regions.Contains(selected))
        {
            _region = null;
            NotifyPropertyChanged(nameof(SelectedRegion));
        }

        NotifyPropertyChanged(nameof(Cards));
        NotifyPropertyChanged(nameof(HasListings));
        Rebuild();
    }

    private static bool MatchesSearch(ProviderListingCardModel card, string text) =>
        Contains(card.DisplayName, text)
        || Contains(card.Id, text)
        || (card.Listing.OmpProviderId is { } omp && Contains(omp, text))
        || (card.Listing.Country is { } code && (Contains(code, text) || Contains(ProviderListingCopy.CountryName(code), text)))
        || (card.Listing.ResidencyNote is { } residency && Contains(residency, text))
        || (card.Note is { } note && Contains(note, text))
        || Contains(card.RegionText, text)
        || Contains(card.KindText, text)
        || card.Routes.Any(route => Contains(route.BaseAddressTemplate, text) || Contains(route.ApiName, text));

    private static bool Contains(string value, string text) => value.Contains(text, StringComparison.OrdinalIgnoreCase);

    private void Rebuild()
    {
        var text = _searchText.Trim();
        bool Search(ProviderListingCardModel card) => text.Length == 0 || MatchesSearch(card, text);
        var sections = new List<ProviderListingSection>();
        foreach (var region in _regions)
        {
            foreach (var kind in KindOrder)
            {
                var cards = _cards
                    .Where(card => card.Region == region && card.Kind == kind && Search(card)
                        && (_region is null || _region == region) && (_kind is null || _kind == kind))
                    .ToArray();
                if (cards.Length > 0)
                {
                    sections.Add(new ProviderListingSection(region, kind, cards));
                }
            }
        }

        _sections = sections;
        _regionChoices =
        [
            ProviderListingCopy.ChoiceText(ProviderListingCopy.AllChoice, _cards.Count(card => Search(card) && (_kind is null || card.Kind == _kind))),
            .. _regions.Select(region => ProviderListingCopy.ChoiceText(
                ProviderListingCopy.RegionTitle(region),
                _cards.Count(card => card.Region == region && Search(card) && (_kind is null || card.Kind == _kind)))),
        ];
        _kindChoices =
        [
            ProviderListingCopy.ChoiceText(ProviderListingCopy.AllChoice, _cards.Count(card => Search(card) && (_region is null || card.Region == _region))),
            .. KindOrder.Select(kind => ProviderListingCopy.ChoiceText(
                ProviderListingCopy.KindTitle(kind),
                _cards.Count(card => card.Kind == kind && Search(card) && (_region is null || card.Region == _region)))),
        ];
        NotifyPropertyChanged(nameof(Sections));
        NotifyPropertyChanged(nameof(VisibleCount));
        NotifyPropertyChanged(nameof(HasNoMatches));
        NotifyPropertyChanged(nameof(RegionChoices));
        NotifyPropertyChanged(nameof(KindChoices));
        NotifyPropertyChanged(nameof(SelectedRegionIndex));
        NotifyPropertyChanged(nameof(SelectedKindIndex));
    }
}
