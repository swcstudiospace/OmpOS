using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Image pin gates (phase 25.4, D10): every container image reference in the local
/// backend compose file and the test-services script carries an `@sha256:` digest.
/// </summary>
public sealed class ImagePinTests
{
    [Fact]
    public void ComposeImageReferences_CarryDigest()
    {
        var path = Path.Combine(ProjectGraphChecker.FindRepoRoot(), "deploy", "compose.yaml");
        File.Exists(path).ShouldBeTrue("expected deploy/compose.yaml");

        var references = ImageReferences(path, ComposeImageReference);
        references.ShouldNotBeEmpty("expected at least one image: line in deploy/compose.yaml");
        references.Where(reference => !reference.Contains("@sha256:", StringComparison.Ordinal))
            .ShouldBeEmpty("every image reference in deploy/compose.yaml must carry an @sha256: digest");
    }

    [Fact]
    public void TestServicesImageReferences_CarryDigest()
    {
        var path = Path.Combine(ProjectGraphChecker.FindRepoRoot(), "scripts", "test-services.sh");
        File.Exists(path).ShouldBeTrue("expected scripts/test-services.sh");

        var references = ImageReferences(path, ScriptImageReference);
        references.ShouldNotBeEmpty("expected at least one *_IMAGE= assignment in scripts/test-services.sh");
        references.Where(reference => !reference.Contains("@sha256:", StringComparison.Ordinal))
            .ShouldBeEmpty("every image reference in scripts/test-services.sh must carry an @sha256: digest");
    }

    /// <summary>The reference of a compose <c>image:</c> line, or null when the line is not one.</summary>
    private static string? ComposeImageReference(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("image:", StringComparison.Ordinal)
            ? FirstToken(trimmed["image:".Length..])
            : null;
    }

    /// <summary>The reference of a shell <c>NAME_IMAGE=</c> assignment, or null when the line is not one.</summary>
    private static string? ScriptImageReference(string line)
    {
        const string Marker = "_IMAGE=";
        var trimmed = line.TrimStart();
        var at = trimmed.IndexOf(Marker, StringComparison.Ordinal);
        if (at <= 0 || !trimmed[..at].All(static c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_'))
        {
            return null;
        }

        return FirstToken(trimmed[(at + Marker.Length)..]).Trim('"', '\'');
    }

    private static string FirstToken(string text)
    {
        var value = text.TrimStart();
        var end = value.IndexOfAny([' ', '\t', '#']);
        return end < 0 ? value.TrimEnd() : value[..end];
    }

    private static List<string> ImageReferences(string path, Func<string, string?> reference)
    {
        var references = new List<string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var value = reference(line);
            if (value is not null)
            {
                references.Add(value);
            }
        }

        return references;
    }
}
