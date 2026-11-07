using Autonogrammer.Api.Cloud;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>environment.json is read current, then legacy, then Cursor, with a warning when the legacy file is involved.</summary>
public sealed class LegacyEnvironmentConfigTests
{
    /// <summary>The four layouts return the expected source, and neither returns null.</summary>
    [Fact]
    public void LegacyEnvironmentSitsBetweenNewAndCursor()
    {
        using var directory = new LocalTemp();
        directory.Write(LegacyBrandNames.ConventionDirectory + "/environment.json", "{\"install\":\"legacy\"}");
        var legacy = EnvironmentConfigFile.ReadFromDirectory(directory.Path).ShouldNotBeNull();
        legacy.Config.ShouldNotBeNull().Install.ShouldBe("legacy");
        legacy.Warnings.ShouldNotBeEmpty();

        directory.Write(".cursor/environment.json", "{\"install\":\"cursor\"}");
        EnvironmentConfigFile.ReadFromDirectory(directory.Path).ShouldNotBeNull().Config.ShouldNotBeNull().Install.ShouldBe("legacy");

        directory.Write(".autonogrammer/environment.json", "{\"install\":\"current\"}");
        var both = EnvironmentConfigFile.ReadFromDirectory(directory.Path).ShouldNotBeNull();
        both.Config.ShouldNotBeNull().Install.ShouldBe("current");
        both.Warnings.ShouldContain(warning => warning.Contains("not merged", StringComparison.Ordinal));
    }

    private sealed class LocalTemp : IDisposable
    {
        public LocalTemp() => Path = Directory.CreateTempSubdirectory("legacy-env-").FullName;

        public string Path { get; }

        public void Write(string relative, string text)
        {
            var path = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
