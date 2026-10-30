using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Registry.Tests;

public sealed class RegistryReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExpectedGenerationEqualToTheExportIsCurrent()
    {
        using var directory = new TempDirectory();
        await WriteTwoMutationsAsync(directory.ExportPath);

        var result = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 2, Token);

        result.Status.ShouldBe(RegistryReadStatus.Current);
        result.Rejection.ShouldBeNull();
        result.Snapshot.ShouldNotBeNull().Generation.ShouldBe(2);
        result.Snapshot.Entries.ShouldBe([new RegistryEntry("alpha", "Renamed", Enabled: true)]);
    }

    [Fact]
    public async Task ExpectedGenerationDifferentFromTheExportIsDriftWithTheExportsEntries()
    {
        using var directory = new TempDirectory();
        await WriteTwoMutationsAsync(directory.ExportPath);
        var before = await File.ReadAllBytesAsync(directory.ExportPath, Token);

        var result = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 1, Token);

        result.Status.ShouldBe(RegistryReadStatus.Drift);
        result.Snapshot.ShouldNotBeNull().Generation.ShouldBe(2);
        result.Snapshot.Entries.ShouldBe([new RegistryEntry("alpha", "Renamed", Enabled: true)]);
        (await File.ReadAllBytesAsync(directory.ExportPath, Token)).ShouldBe(before);
    }

    [Fact]
    public async Task AbsentExportIsMissing()
    {
        using var directory = new TempDirectory();

        var result = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 0, Token);

        result.Status.ShouldBe(RegistryReadStatus.Missing);
        result.Snapshot.ShouldBeNull();
        result.Rejection.ShouldBeNull();
    }

    [Fact]
    public async Task ChecksumMismatchIsRejectedAndTheWriterRefusesToOpen()
    {
        using var directory = new TempDirectory();
        using (var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token))
        {
            await writer.AddAsync("alpha", "display one", Token);
        }

        var original = await File.ReadAllTextAsync(directory.ExportPath, Token);
        var tampered = original.Replace("\"display one\"", "\"display two\"", StringComparison.Ordinal);
        tampered.Equals(original, StringComparison.Ordinal).ShouldBeFalse();
        await File.WriteAllTextAsync(directory.ExportPath, tampered, Token);

        var result = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 1, Token);

        result.Status.ShouldBe(RegistryReadStatus.Rejected);
        result.Rejection.ShouldBe(RegistryRejectionKind.ChecksumMismatch);
        result.Snapshot.ShouldBeNull();

        await Should.ThrowAsync<InvalidRegistryExportException>(
            () => RegistryWriter.OpenAsync(directory.ExportPath, null, Token));
        (await File.ReadAllTextAsync(directory.ExportPath, Token)).ShouldBe(tampered);
    }

    [Fact]
    public async Task TruncatedExportIsRejectedAsMalformedWithoutThrowing()
    {
        using var directory = new TempDirectory();
        await WriteTwoMutationsAsync(directory.ExportPath);
        var bytes = await File.ReadAllBytesAsync(directory.ExportPath, Token);
        await File.WriteAllBytesAsync(directory.ExportPath, bytes[..(bytes.Length / 2)], Token);

        var result = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 2, Token);

        result.Status.ShouldBe(RegistryReadStatus.Rejected);
        result.Rejection.ShouldBe(RegistryRejectionKind.Malformed);
        result.Snapshot.ShouldBeNull();
    }

    [Fact]
    public async Task NonJsonContentIsRejectedAsMalformed()
    {
        using var directory = new TempDirectory();
        await File.WriteAllTextAsync(directory.ExportPath, "registry: not json", Token);

        var result = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 0, Token);

        result.Status.ShouldBe(RegistryReadStatus.Rejected);
        result.Rejection.ShouldBe(RegistryRejectionKind.Malformed);
    }

    [Fact]
    public async Task UnknownMemberIsRejectedAsMalformed()
    {
        using var directory = new TempDirectory();
        await WriteTwoMutationsAsync(directory.ExportPath);
        var original = await File.ReadAllTextAsync(directory.ExportPath, Token);
        await File.WriteAllTextAsync(directory.ExportPath, "{\"extra\":true," + original[1..], Token);

        var result = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 2, Token);

        result.Status.ShouldBe(RegistryReadStatus.Rejected);
        result.Rejection.ShouldBe(RegistryRejectionKind.Malformed);
    }

    [Fact]
    public async Task NegativeExpectedGenerationIsAnArgumentError()
    {
        using var directory = new TempDirectory();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: -1, Token));
    }

    private static async Task WriteTwoMutationsAsync(string exportPath)
    {
        using var writer = await RegistryWriter.OpenAsync(exportPath, null, Token).ConfigureAwait(false);
        await writer.AddAsync("alpha", "Alpha", Token).ConfigureAwait(false);
        await writer.UpdateAsync("alpha", "Renamed", Token).ConfigureAwait(false);
    }
}
