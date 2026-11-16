using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>The desktop's bundle record is read whole or refused: every field has its shape, and a field that could reach a script is checked first.</summary>
public sealed class PluginBundleRecordReaderTests
{
    private const string Valid =
        "[{\"dir\":\"gsd-core-omp\",\"source\":\"in-repo\",\"commit\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"licenseFile\":true,\"needsInstall\":false},"
        + "{\"dir\":\"claude-ultrathink\",\"source\":\"https://github.com/swcstudiospace/ultrathink.git\",\"commit\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"licenseFile\":false,\"needsInstall\":true}]";

    /// <summary>A valid record gives its records in file order with every field.</summary>
    [Fact]
    public void ValidRecordParsesInFileOrder()
    {
        var records = PluginBundleRecordReader.Parse(Valid);

        records.Count.ShouldBe(2);
        records[0].ShouldBe(new PluginBundleRecord("gsd-core-omp", "in-repo", new string('a', 40), LicenseFile: true, NeedsInstall: false));
        records[1].Source.ShouldBe("https://github.com/swcstudiospace/ultrathink.git");
        records[1].NeedsInstall.ShouldBeTrue();
        records[1].LicenseFile.ShouldBeFalse();
    }

    /// <summary>An empty array is an empty bundle, not an error.</summary>
    [Fact]
    public void EmptyArrayIsAnEmptyBundle()
    {
        PluginBundleRecordReader.Parse("[]").ShouldBeEmpty();
    }

    /// <summary>A record that is not a JSON array is refused.</summary>
    [Fact]
    public void NonArrayIsRefused()
    {
        Should.Throw<InvalidDataException>(() => { _ = PluginBundleRecordReader.Parse("{}"); });
    }

    /// <summary>A commit that is not a full lowercase commit name is refused, because it is placed in a script.</summary>
    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("abc")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;rm")]
    public void CommitThatIsNotAFullCommitIsRefused(string commit)
    {
        var json = "[{\"dir\":\"x\",\"source\":\"in-repo\",\"commit\":\"" + commit + "\",\"licenseFile\":true,\"needsInstall\":false}]";

        Should.Throw<InvalidDataException>(() => { _ = PluginBundleRecordReader.Parse(json); });
    }

    /// <summary>A source that is neither in-repo nor a plain https git address is refused, so no shell syntax reaches a clone.</summary>
    [Theory]
    [InlineData("http://github.com/a/b.git")]
    [InlineData("https://github.com/a/b.git;rm -rf ~")]
    [InlineData("https://github.com/a/$(id).git")]
    public void SourceWithShellSyntaxIsRefused(string source)
    {
        var json = "[{\"dir\":\"x\",\"source\":\"" + source + "\",\"commit\":\"" + new string('a', 40) + "\",\"licenseFile\":true,\"needsInstall\":false}]";

        Should.Throw<InvalidDataException>(() => { _ = PluginBundleRecordReader.Parse(json); });
    }

    /// <summary>A directory that is a path, or starts with a dot, is refused.</summary>
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData(".hidden")]
    public void DirectoryThatIsNotOnePlainNameIsRefused(string dir)
    {
        var json = "[{\"dir\":\"" + dir + "\",\"source\":\"in-repo\",\"commit\":\"" + new string('a', 40) + "\",\"licenseFile\":true,\"needsInstall\":false}]";

        Should.Throw<InvalidDataException>(() => { _ = PluginBundleRecordReader.Parse(json); });
    }

    /// <summary>A flag that is missing or not a boolean is refused rather than defaulted.</summary>
    [Fact]
    public void MissingFlagIsRefused()
    {
        var json = "[{\"dir\":\"x\",\"source\":\"in-repo\",\"commit\":\"" + new string('a', 40) + "\",\"licenseFile\":true}]";

        Should.Throw<InvalidDataException>(() => { _ = PluginBundleRecordReader.Parse(json); });
    }

    /// <summary>Two records for one directory are refused, because only one could be the one installed.</summary>
    [Fact]
    public void DuplicateDirectoryIsRefused()
    {
        var entry = "{\"dir\":\"x\",\"source\":\"in-repo\",\"commit\":\"" + new string('a', 40) + "\",\"licenseFile\":true,\"needsInstall\":false}";

        Should.Throw<InvalidDataException>(() => { _ = PluginBundleRecordReader.Parse("[" + entry + "," + entry + "]"); });
    }

    /// <summary>A record's JSON form holds the same five fields the bundle record holds.</summary>
    [Fact]
    public void RecordJsonCarriesTheFiveFields()
    {
        var record = new PluginBundleRecord("x", "in-repo", new string('a', 40), LicenseFile: true, NeedsInstall: false);

        var json = record.ToJson();

        json["dir"]!.GetValue<string>().ShouldBe("x");
        json["commit"]!.GetValue<string>().ShouldBe(new string('a', 40));
        json["licenseFile"]!.GetValue<bool>().ShouldBeTrue();
        json["needsInstall"]!.GetValue<bool>().ShouldBeFalse();
        json.Count.ShouldBe(5);
    }
}
