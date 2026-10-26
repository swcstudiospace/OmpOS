namespace OmpOS.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// Loads the trimmed real and hand-written outputs of the workflow tool from <c>TestData/gsd</c>.
/// </summary>
internal static class GsdFixtures
{
    /// <summary>Reads a fixture file.</summary>
    /// <param name="name">The file name inside <c>TestData/gsd</c>.</param>
    public static string Load(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "gsd", name));
}
