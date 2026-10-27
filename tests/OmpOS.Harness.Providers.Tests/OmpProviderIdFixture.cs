using System.Reflection;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>The provider ids omp 18.12.6 knows, as recorded in <c>Fixtures/omp-provider-ids.txt</c> (the bundled catalogue plus the env table of its provider documentation).</summary>
internal static class OmpProviderIdFixture
{
    private const string ResourceName = "OmpOS.Harness.Providers.Tests.Fixtures.omp-provider-ids.txt";

    /// <summary>Reads the recorded ids.</summary>
    /// <returns>The ids, without the comment lines.</returns>
    public static HashSet<string> Read()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The omp provider id fixture is not embedded.");
        using var reader = new StreamReader(stream);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && trimmed[0] != '#')
            {
                _ = ids.Add(trimmed);
            }
        }

        return ids;
    }
}
