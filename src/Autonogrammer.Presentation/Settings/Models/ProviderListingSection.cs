namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>One heading of the provider listing with its cards: a region and a kind, such as "Europe: Coding plans".</summary>
public sealed class ProviderListingSection
{
    /// <summary>Initializes a new instance of the <see cref="ProviderListingSection"/> class.</summary>
    /// <param name="region">The region of the section.</param>
    /// <param name="kind">The kind of the section.</param>
    /// <param name="cards">The cards in it, in the order of the listing.</param>
    internal ProviderListingSection(ProviderListingRegion region, ProviderListingKind kind, IReadOnlyList<ProviderListingCardModel> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        Region = region;
        Kind = kind;
        Cards = cards;
    }

    /// <summary>Gets the region of the section.</summary>
    public ProviderListingRegion Region { get; }

    /// <summary>Gets the kind of the section.</summary>
    public ProviderListingKind Kind { get; }

    /// <summary>Gets the heading.</summary>
    public string Heading => ProviderListingCopy.SectionHeading(Region, Kind);

    /// <summary>Gets the cards.</summary>
    public IReadOnlyList<ProviderListingCardModel> Cards { get; }
}
