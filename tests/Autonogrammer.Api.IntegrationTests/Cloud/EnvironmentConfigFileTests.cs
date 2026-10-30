using Autonogrammer.Api.Cloud;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>The environment file reader and the resolution order: schema checks, Cursor's path semantics, fixtures on disk, and personal over repository.</summary>
public sealed class EnvironmentConfigFileTests
{
    private const string Full =
        "{\"$schema\":\"https://www.cursor.com/schemas/environment.schema.json\",\"snapshot\":\"snap-1\",\"install\":\"npm ci\",\"start\":\"npm start\"," +
        "\"terminals\":[{\"name\":\"web\",\"command\":\"npm run dev\",\"description\":\"dev server\"},{\"name\":\"tests\",\"command\":\"npm test -- --watch\"}]," +
        "\"build\":{\"dockerfile\":\"Dockerfile\",\"context\":\".\"}}";

    /// <summary>A complete file is read into every member; the schema member is accepted and ignored.</summary>
    [Fact]
    public void Parse_ReadsEveryMember()
    {
        var result = EnvironmentConfigFile.Parse(Full, ".autonogrammer/environment.json");

        var config = result.Config.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
        config.Snapshot.ShouldBe("snap-1");
        config.Install.ShouldBe("npm ci");
        config.Start.ShouldBe("npm start");
        config.Terminals.Select(static terminal => terminal.Name).ToArray().ShouldBe(["web", "tests"]);
        config.Terminals[1].Command.ShouldBe("npm test -- --watch");
        config.Source.ShouldBe(".autonogrammer/environment.json");
    }

    /// <summary>Build paths are relative to the folder of the file, so from .cursor both a dot and a double dot mean the repository root.</summary>
    [Theory]
    [InlineData(".cursor/environment.json", ".", ".")]
    [InlineData(".cursor/environment.json", "..", ".")]
    [InlineData(".cursor/environment.json", "../docker/Dockerfile", "docker/Dockerfile")]
    [InlineData(".cursor/environment.json", "Dockerfile", ".cursor/Dockerfile")]
    [InlineData(".autonogrammer/environment.json", "./a/../b/Dockerfile", ".autonogrammer/b/Dockerfile")]
    public void Build_PathsAreRelativeToTheFile(string source, string path, string expected)
    {
        var json = "{\"build\":{\"dockerfile\":\"" + path + "\"}}";

        var config = EnvironmentConfigFile.Parse(json, source).Config.ShouldNotBeNull();

        config.Build.ShouldNotBeNull().Dockerfile.ShouldBe(expected);
    }

    /// <summary>A build path that leaves the repository, is absolute, or uses backslashes is an error.</summary>
    [Theory]
    [InlineData("../../outside")]
    [InlineData("/etc/passwd")]
    [InlineData("..\\\\windows")]
    public void Build_PathsThatEscapeAreRefused(string path)
    {
        var result = EnvironmentConfigFile.Parse("{\"build\":{\"context\":\"" + path + "\"}}", ".cursor/environment.json");

        result.Config.ShouldBeNull();
        result.Errors.ShouldHaveSingleItem().ShouldContain("build.context", Case.Sensitive);
    }

    /// <summary>Wrong types, missing terminal members, oversize values and bad JSON are each refused with an error that names the member and repeats no value.</summary>
    [Theory]
    [InlineData("[]", "JSON object")]
    [InlineData("{not json", "not valid JSON")]
    [InlineData("{\"install\":42}", "'install' must be a string")]
    [InlineData("{\"start\":{}}", "'start' must be a string")]
    [InlineData("{\"terminals\":{}}", "'terminals' must be an array")]
    [InlineData("{\"terminals\":[1]}", "terminals[0] must be an object")]
    [InlineData("{\"terminals\":[{\"command\":\"x\"}]}", "terminals[0].name")]
    [InlineData("{\"terminals\":[{\"name\":\"a\"}]}", "terminals[0].command")]
    [InlineData("{\"build\":[]}", "'build' must be an object")]
    [InlineData("{\"install\":\"a\\u0000b\"}", "NUL")]
    public void Parse_RefusesInvalidFiles(string json, string fragment)
    {
        var result = EnvironmentConfigFile.Parse(json, ".autonogrammer/environment.json");

        result.Config.ShouldBeNull();
        result.Errors.ShouldNotBeEmpty();
        result.Errors.Any(error => error.Contains(fragment, StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>A file over the size limit, a command over its limit and more than sixteen terminals are refused.</summary>
    [Fact]
    public void Parse_EnforcesTheLimits()
    {
        var huge = "{\"install\":\"" + new string('x', 70_000) + "\"}";
        var longCommand = "{\"install\":\"" + new string('x', 5_000) + "\"}";
        var terminals = string.Join(',', Enumerable.Range(0, 17).Select(static index => "{\"name\":\"t" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\",\"command\":\"c\"}"));

        EnvironmentConfigFile.Parse(huge, "x").Config.ShouldBeNull();
        EnvironmentConfigFile.Parse(longCommand, "x").Config.ShouldBeNull();
        EnvironmentConfigFile.Parse("{\"terminals\":[" + terminals + "]}", "x").Errors.ShouldContain("'terminals' holds more than 16 entries.", StringComparer.Ordinal);
    }

    /// <summary>Unknown members are warned about, not refused, and an empty command counts as none.</summary>
    [Fact]
    public void Parse_WarnsAboutUnknownMembers()
    {
        var result = EnvironmentConfigFile.Parse("{\"install\":\"\",\"extra\":true}", ".autonogrammer/environment.json");

        result.Config.ShouldNotBeNull().Install.ShouldBeNull();
        result.Warnings.ShouldHaveSingleItem();
    }

    /// <summary>The reader takes the Autonogrammer file before Cursor's from a checked-out directory, and reads nothing it does not find.</summary>
    [Fact]
    public void ReadFromDirectory_PrefersTheAutonogrammerFile()
    {
        using var directory = new TempDirectory();
        directory.Write(".cursor/environment.json", "{\"install\":\"from cursor\"}");

        var cursorOnly = EnvironmentConfigFile.ReadFromDirectory(directory.Path).ShouldNotBeNull();
        cursorOnly.Config.ShouldNotBeNull().Install.ShouldBe("from cursor");
        cursorOnly.Config.Source.ShouldBe(".cursor/environment.json");

        directory.Write(".autonogrammer/environment.json", "{\"install\":\"from autonogrammer\"}");
        var both = EnvironmentConfigFile.ReadFromDirectory(directory.Path).ShouldNotBeNull();
        both.Config.ShouldNotBeNull().Install.ShouldBe("from autonogrammer");

        using var empty = new TempDirectory();
        EnvironmentConfigFile.ReadFromDirectory(empty.Path).ShouldBeNull();
    }

    /// <summary>A file on disk that is invalid is reported as such, not skipped for the next one.</summary>
    [Fact]
    public void ReadFromDirectory_ReportsAnInvalidFirstFile()
    {
        using var directory = new TempDirectory();
        directory.Write(".autonogrammer/environment.json", "{\"install\":1}");
        directory.Write(".cursor/environment.json", "{\"install\":\"fine\"}");

        var result = EnvironmentConfigFile.ReadFromDirectory(directory.Path).ShouldNotBeNull();

        result.Config.ShouldBeNull();
        result.Errors.ShouldNotBeEmpty();
    }

    /// <summary>The order is personal, then the Autonogrammer file, then Cursor's file; a repository file counts only for a single-repository agent; an invalid chosen file is reported.</summary>
    [Fact]
    public void Resolve_FollowsThePersonalThenRepositoryOrder()
    {
        var personal = new EnvironmentConfig(null, "personal install", null, [], null, "personal");
        const string autonogrammer = "{\"install\":\"autonogrammer install\"}";
        const string cursor = "{\"install\":\"cursor install\"}";

        EnvironmentConfigResolver.Resolve(personal, 1, autonogrammer, cursor).Config.ShouldNotBeNull().Install.ShouldBe("personal install");
        EnvironmentConfigResolver.Resolve(null, 1, autonogrammer, cursor).Config.ShouldNotBeNull().Install.ShouldBe("autonogrammer install");
        EnvironmentConfigResolver.Resolve(null, 1, null, cursor).Config.ShouldNotBeNull().Install.ShouldBe("cursor install");
        EnvironmentConfigResolver.Resolve(null, 2, autonogrammer, cursor).Config.ShouldBeNull();
        EnvironmentConfigResolver.Resolve(null, 1, null, null).Config.ShouldBeNull();

        var invalid = EnvironmentConfigResolver.Resolve(null, 1, "{\"install\":1}", cursor);
        invalid.Config.ShouldBeNull();
        invalid.Errors.ShouldHaveSingleItem().ShouldStartWith(".autonogrammer/environment.json:", Case.Sensitive);
    }

    /// <summary>Dockerfile builds and snapshots are parsed and reported as unsupported on a Railway worker; nothing is run.</summary>
    [Fact]
    public void Resolve_WarnsThatBuildAndSnapshotAreNotSupported()
    {
        var resolved = EnvironmentConfigResolver.Resolve(null, 1, Full, null);

        resolved.Config.ShouldNotBeNull();
        resolved.Warnings.Count.ShouldBe(2);
        resolved.Warnings.Any(static warning => warning.Contains("Docker", StringComparison.Ordinal)).ShouldBeTrue();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "autonogrammer-env-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Write(string relative, string content)
        {
            var target = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.WriteAllText(target, content);
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
