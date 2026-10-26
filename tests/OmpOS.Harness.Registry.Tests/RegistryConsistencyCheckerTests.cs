using System.Collections.Immutable;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Registry.Tests;

public sealed class RegistryConsistencyCheckerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UiSnapshotAndOmpViewReportTheSameGeneration()
    {
        using var directory = new TempDirectory();
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);
        await writer.AddAsync("alpha", "Alpha", Token);
        await writer.AddAsync("beta", "Beta", Token);
        await writer.DisableAsync("alpha", Token);

        var report = await RegistryConsistencyChecker.CheckAsync(
            writer.Snapshot,
            new FileOmpRegistryView(directory.ExportPath),
            Token);

        report.Status.ShouldBe(RegistryConsistencyStatus.Consistent);
        report.UiGeneration.ShouldBe(3);
        report.OmpGeneration.ShouldBe(3);
        report.UiChecksum.ShouldNotBeNullOrEmpty();
        report.OmpChecksum.ShouldBe(report.UiChecksum);
        report.OmpRejection.ShouldBeNull();
    }

    [Fact]
    public async Task UiSnapshotBehindTheExportIsReportedAsGenerationDrift()
    {
        using var directory = new TempDirectory();
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);
        await writer.AddAsync("alpha", "Alpha", Token);
        var stale = writer.Snapshot;
        await writer.AddAsync("beta", "Beta", Token);

        var report = await RegistryConsistencyChecker.CheckAsync(
            stale,
            new FileOmpRegistryView(directory.ExportPath),
            Token);

        report.Status.ShouldBe(RegistryConsistencyStatus.GenerationDrift);
        report.UiGeneration.ShouldBe(1);
        report.OmpGeneration.ShouldBe(2);
        report.OmpChecksum.ShouldNotBe(report.UiChecksum, StringComparer.Ordinal);
    }

    [Fact]
    public async Task SameGenerationWithDifferentEntriesIsReportedAsChecksumMismatch()
    {
        using var directory = new TempDirectory();
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);
        await writer.AddAsync("alpha", "Alpha", Token);

        var diverged = new RegistrySnapshot(1, [new RegistryEntry("alpha", "Not the exported name", Enabled: true)]);

        var report = await RegistryConsistencyChecker.CheckAsync(
            diverged,
            new FileOmpRegistryView(directory.ExportPath),
            Token);

        report.Status.ShouldBe(RegistryConsistencyStatus.ChecksumMismatch);
        report.UiGeneration.ShouldBe(1);
        report.OmpGeneration.ShouldBe(1);
        report.OmpChecksum.ShouldNotBe(report.UiChecksum, StringComparer.Ordinal);
    }

    [Fact]
    public async Task AbsentOmpExportIsReportedAsMissingWithoutAGeneration()
    {
        using var directory = new TempDirectory();
        var ui = new RegistrySnapshot(0, ImmutableArray<RegistryEntry>.Empty);

        var report = await RegistryConsistencyChecker.CheckAsync(
            ui,
            new FileOmpRegistryView(directory.ExportPath),
            Token);

        report.Status.ShouldBe(RegistryConsistencyStatus.OmpMissing);
        report.OmpGeneration.ShouldBeNull();
        report.OmpChecksum.ShouldBeNull();
    }

    [Fact]
    public async Task TamperedOmpExportIsReportedAsRejectedWithItsReason()
    {
        using var directory = new TempDirectory();
        using var writer = await RegistryWriter.OpenAsync(directory.ExportPath, null, Token);
        await writer.AddAsync("alpha", "display one", Token);

        var original = await File.ReadAllTextAsync(directory.ExportPath, Token);
        var tampered = original.Replace("\"display one\"", "\"display two\"", StringComparison.Ordinal);
        tampered.Equals(original, StringComparison.Ordinal).ShouldBeFalse();
        await File.WriteAllTextAsync(directory.ExportPath, tampered, Token);

        var report = await RegistryConsistencyChecker.CheckAsync(
            writer.Snapshot,
            new FileOmpRegistryView(directory.ExportPath),
            Token);

        report.Status.ShouldBe(RegistryConsistencyStatus.OmpRejected);
        report.OmpRejection.ShouldBe(RegistryRejectionKind.ChecksumMismatch);
        report.OmpGeneration.ShouldBeNull();
    }

    [Fact]
    public async Task OmpViewIsAskedForTheGenerationTheUiShows()
    {
        var view = new RecordingView(new RegistryReadResult(RegistryReadStatus.Missing, null, null));
        var ui = new RegistrySnapshot(7, ImmutableArray<RegistryEntry>.Empty);

        await RegistryConsistencyChecker.CheckAsync(ui, view, Token);

        view.RequestedGenerations.ShouldBe([7L]);
    }

    private sealed class RecordingView(RegistryReadResult result) : IOmpRegistryView
    {
        public List<long> RequestedGenerations { get; } = [];

        public Task<RegistryReadResult> ReadAsync(long expectedGeneration, CancellationToken cancellationToken)
        {
            RequestedGenerations.Add(expectedGeneration);
            return Task.FromResult(result);
        }
    }
}
