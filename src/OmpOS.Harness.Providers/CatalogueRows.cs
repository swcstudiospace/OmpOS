using System.Collections.Frozen;
using System.Text.Json;

namespace OmpOS.Harness.Providers;

/// <summary>
/// The data-driven provider catalogue: every provider, aggregator, coding plan and local runtime the Models page offers, read once
/// from the <c>Data/catalogue-*.json</c> files embedded in this assembly. The rows replace nothing in <see cref="ProviderCatalogue"/>:
/// that class keeps the typed entries the probes and the registry use, and <see cref="ProviderIdMap"/> maps each of its ids to a row.
/// A malformed data file fails the first access with an <see cref="InvalidOperationException"/>; tests keep the data valid.
/// </summary>
public static class CatalogueRows
{
    private const string ResourcePrefix = "OmpOS.Harness.Providers.Data.catalogue-";

    private static readonly IReadOnlyList<CatalogueRow> Loaded = Load();

    private static readonly FrozenDictionary<string, CatalogueRow> ById =
        Loaded.ToFrozenDictionary(row => row.Id, StringComparer.Ordinal);

    /// <summary>Gets every row, in the order of the data files (by file name: cn, eu, us) and, inside a file, as written. Pages group them by region and kind.</summary>
    public static IReadOnlyList<CatalogueRow> All => Loaded;

    /// <summary>Returns the row with the id, or null when there is none.</summary>
    /// <param name="id">The row id.</param>
    /// <returns>The row, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    public static CatalogueRow? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return ById.GetValueOrDefault(id);
    }

    /// <summary>Returns the row whose omp provider id is <paramref name="ompProviderId"/>, or null when no row has it.</summary>
    /// <param name="ompProviderId">The id omp knows the provider by.</param>
    /// <returns>The row, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ompProviderId"/> is null.</exception>
    public static CatalogueRow? FindByOmpProviderId(string ompProviderId)
    {
        ArgumentNullException.ThrowIfNull(ompProviderId);
        return Loaded.FirstOrDefault(row => string.Equals(row.OmpProviderId, ompProviderId, StringComparison.Ordinal));
    }

    private static List<CatalogueRow> Load()
    {
        var assembly = typeof(CatalogueRows).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (names.Length == 0)
        {
            throw new InvalidOperationException("The provider catalogue data files are not embedded in the assembly.");
        }

        var rows = new List<CatalogueRow>();
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"The embedded catalogue file {name} could not be opened.");
            try
            {
                var document = JsonSerializer.Deserialize(stream, CatalogueJsonContext.Default.CatalogueDocument)
                    ?? throw new InvalidOperationException($"The embedded catalogue file {name} is empty.");
                rows.AddRange(document.Rows);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"The embedded catalogue file {name} is malformed: {exception.Message}", exception);
            }
        }

        return rows;
    }
}
