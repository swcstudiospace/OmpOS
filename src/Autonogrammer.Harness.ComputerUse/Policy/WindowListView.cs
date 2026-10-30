namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Titles for granted applications and category counts for the rest.</summary>
/// <param name="Titles">Titles the model may see.</param>
/// <param name="UngrantedCategoryCounts">Counts by category. No titles.</param>
public sealed record WindowListView(IReadOnlyList<string> Titles, IReadOnlyDictionary<string, int> UngrantedCategoryCounts);
