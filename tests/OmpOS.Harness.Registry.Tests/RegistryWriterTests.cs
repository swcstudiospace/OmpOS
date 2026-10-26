using Shouldly;
using Xunit;

namespace OmpOS.Harness.Registry.Tests;

public sealed class RegistryWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachMutationIncrementsTheGenerationExactlyOnce()
    {
        using var directory = new TempDirectory();
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);

        writer.Snapshot.Generation.ShouldBe(0);

        await writer.AddAsync("alpha", "Alpha", Token);
        writer.Snapshot.Generation.ShouldBe(1);

        await writer.UpdateAsync("alpha", "Alpha renamed", Token);
        writer.Snapshot.Generation.ShouldBe(2);

        await writer.DisableAsync("alpha", Token);
        writer.Snapshot.Generation.ShouldBe(3);

        await writer.EnableAsync("alpha", Token);
        writer.Snapshot.Generation.ShouldBe(4);

        await writer.RemoveAsync("alpha", Token);
        writer.Snapshot.Generation.ShouldBe(5);
        writer.Snapshot.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnablementAndNameChangeIndependently()
    {
        using var directory = new TempDirectory();
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);

        await writer.AddAsync("alpha", "Alpha", Token);
        await writer.DisableAsync("alpha", Token);
        writer.Snapshot.Entries.ShouldBe([new RegistryEntry("alpha", "Alpha", Enabled: false)]);

        await writer.UpdateAsync("alpha", "Renamed", Token);
        writer.Snapshot.Entries.ShouldBe([new RegistryEntry("alpha", "Renamed", Enabled: false)]);
    }

    [Fact]
    public async Task RejectedMutationsLeaveTheGenerationAndEntriesUnchanged()
    {
        using var directory = new TempDirectory();
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);
        await writer.AddAsync("alpha", "Alpha", Token);

        await Should.ThrowAsync<RegistryEntryExistsException>(() => writer.AddAsync("alpha", "Again", Token));
        await Should.ThrowAsync<RegistryEntryNotFoundException>(() => writer.RemoveAsync("missing", Token));
        await Should.ThrowAsync<RegistryEntryNotFoundException>(() => writer.UpdateAsync("missing", "x", Token));
        await Should.ThrowAsync<RegistryEntryNotFoundException>(() => writer.EnableAsync("missing", Token));
        await Should.ThrowAsync<RegistryEntryNotFoundException>(() => writer.DisableAsync("missing", Token));

        writer.Snapshot.Generation.ShouldBe(1);
        writer.Snapshot.Entries.ShouldBe([new RegistryEntry("alpha", "Alpha", Enabled: true)]);
    }

    [Fact]
    public async Task EntriesAreOrderedByIdentifierUsingOrdinalComparison()
    {
        using var directory = new TempDirectory();
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);

        await writer.AddAsync("beta", "Beta", Token);
        await writer.AddAsync("alpha", "Alpha", Token);
        await writer.AddAsync("Zeta", "Zeta", Token);

        writer.Snapshot.Entries.Select(entry => entry.Id).ShouldBe(["Zeta", "alpha", "beta"]);
    }

    [Fact]
    public async Task InterruptedWriteLeavesThePreviousExportReadableAndUnchanged()
    {
        using var directory = new TempDirectory();
        using (var seed = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token))
        {
            await seed.AddAsync("alpha", "Alpha", Token);
        }

        var before = await File.ReadAllBytesAsync(directory.ExportPath, Token);

        using var interrupted = await RegistryWriter.OpenAsync(
            directory.ExportPath,
            new ScriptedFileSystem(failReplace: true),
            Token);

        await Should.ThrowAsync<IOException>(() => interrupted.AddAsync("beta", "Beta", Token));

        (await File.ReadAllBytesAsync(directory.ExportPath, Token)).ShouldBe(before);
        Directory.GetFiles(directory.Path).Select(Path.GetFileName).ShouldBe(["registry.json"]);

        interrupted.Snapshot.Generation.ShouldBe(1);
        interrupted.Snapshot.Entries.Select(entry => entry.Id).ShouldBe(["alpha"]);

        var read = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 1, Token);
        read.Status.ShouldBe(RegistryReadStatus.Current);
        read.Snapshot.ShouldNotBeNull().Entries.Select(entry => entry.Id).ShouldBe(["alpha"]);
    }

    [Fact]
    public async Task ConcurrentMutationsSerializeAndBothAreReflected()
    {
        using var directory = new TempDirectory();
        var fileSystem = new ScriptedFileSystem(writeDelay: TimeSpan.FromMilliseconds(25));
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, fileSystem, Token);

        await Task.WhenAll(
            writer.AddAsync("alpha", "Alpha", Token),
            writer.AddAsync("beta", "Beta", Token));

        fileSystem.MaxConcurrentWrites.ShouldBe(1);
        writer.Snapshot.Generation.ShouldBe(2);
        writer.Snapshot.Entries.Select(entry => entry.Id).ShouldBe(["alpha", "beta"]);

        var read = await RegistryReader.ReadAsync(directory.ExportPath, expectedGeneration: 2, Token);
        read.Status.ShouldBe(RegistryReadStatus.Current);
        read.Snapshot.ShouldNotBeNull().Entries.Select(entry => entry.Id).ShouldBe(["alpha", "beta"]);
    }

    [Fact]
    public async Task ReopenedWriterResumesAtTheSavedGeneration()
    {
        using var directory = new TempDirectory();
        using (var first = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token))
        {
            await first.AddAsync("alpha", "Alpha", Token);
            await first.AddAsync("beta", "Beta", Token);
            await first.DisableAsync("alpha", Token);
        }

        using var reopened = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);
        reopened.Snapshot.Generation.ShouldBe(3);
        reopened.Snapshot.Entries.ShouldBe(
        [
            new RegistryEntry("alpha", "Alpha", Enabled: false),
            new RegistryEntry("beta", "Beta", Enabled: true),
        ]);

        await reopened.AddAsync("gamma", "Gamma", Token);
        reopened.Snapshot.Generation.ShouldBe(4);
    }

    [Fact]
    public async Task OpeningOverAMalformedExportRefusesAndLeavesTheFileUntouched()
    {
        using var directory = new TempDirectory();
        await File.WriteAllTextAsync(directory.ExportPath, "{ not an export", Token);
        var before = await File.ReadAllBytesAsync(directory.ExportPath, Token);

        await Should.ThrowAsync<InvalidRegistryExportException>(
            () => RegistryWriter.OpenAsync(directory.ExportPath, null, Token));

        (await File.ReadAllBytesAsync(directory.ExportPath, Token)).ShouldBe(before);
    }
}
