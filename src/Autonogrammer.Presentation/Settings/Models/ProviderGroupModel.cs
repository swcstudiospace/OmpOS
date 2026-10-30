namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>One group of provider cards, after the search filter. A group with no card left is not listed.</summary>
public sealed class ProviderGroupModel
{
    /// <summary>Initializes a new instance of the <see cref="ProviderGroupModel"/> class.</summary>
    /// <param name="group">The group.</param>
    /// <param name="cards">The cards in it.</param>
    internal ProviderGroupModel(ProviderGroup group, IReadOnlyList<ProviderCardModel> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        Group = group;
        Cards = cards;
    }

    /// <summary>Gets the group.</summary>
    public ProviderGroup Group { get; }

    /// <summary>Gets the heading.</summary>
    public string Title => ModelsCopy.GroupTitle(Group);

    /// <summary>Gets the sentence under the heading.</summary>
    public string Description => ModelsCopy.GroupDescription(Group);

    /// <summary>Gets the cards, in the order the source listed them.</summary>
    public IReadOnlyList<ProviderCardModel> Cards { get; }
}
