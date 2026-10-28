using OmpOS.Native.Views;

namespace OmpOS.Native.Tests.About;

/// <summary>An <see cref="IUrlLauncher"/> that records the addresses it is asked to open instead of starting a browser.</summary>
internal sealed class FakeUrlLauncher : IUrlLauncher
{
    /// <summary>Gets the addresses requested, oldest first.</summary>
    internal List<Uri> Opened { get; } = [];

    /// <summary>Gets or sets the answer: <see langword="true"/> when a browser could be started.</summary>
    internal bool Succeeds { get; set; } = true;

    /// <inheritdoc/>
    public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken)
    {
        Opened.Add(uri);
        return Task.FromResult(Succeeds);
    }
}
