using OmpOS.Presentation.Settings.Ignore;

namespace OmpOS.Native.Tests.Settings;

/// <summary>A scripted source of the Ignore page.</summary>
internal sealed class FakeIgnoreSource : FakeSettingsSource<IgnoreSettingsValues>, IIgnoreSettingsSource
{
    /// <summary>Creates a source whose saved values are the defaults.</summary>
    public FakeIgnoreSource()
        : base(IgnoreSettingsValues.Defaults)
    {
    }

    /// <summary>Gets or sets the files the source lists.</summary>
    public IReadOnlyList<IgnoreFileRow> Files { get; set; } = [];

    /// <inheritdoc />
    public IReadOnlyList<string> DefaultPatterns { get; } = [".git/", "node_modules/", "*.png"];

    /// <inheritdoc />
    public Task<IgnoreSettingsValues> LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(IgnoreSettingsValues values, CancellationToken cancellationToken) => SaveCoreAsync(values);

    /// <inheritdoc />
    public Task<IReadOnlyList<IgnoreFileRow>> FoundFilesAsync(CancellationToken cancellationToken) => Task.FromResult(Files);
}
