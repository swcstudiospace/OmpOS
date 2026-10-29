using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Presentation.Composer.Attachments;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Sessions;

/// <summary>
/// Tells the image attachments of a composer what the session can take: the engine's <c>promptCapabilities.image</c> (read from its <see cref="IOmpModelControl"/> once the engine has answered <c>initialize</c>) and whether
/// the selected model reads images (when a lookup knows it). It pushes again whenever the control reports a change, also from another thread, so the Attach button follows a model switch.
/// </summary>
internal sealed class ImageInputBinding : IDisposable
{
    private readonly IOmpModelControl? _control;
    private readonly Func<string, VisionSupport>? _modelVision;
    private readonly ComposerAttachmentsModel _images;
    private int _disposed;

    /// <summary>Creates the binding and pushes what the control reports now.</summary>
    /// <param name="images">The attachments of the composer.</param>
    /// <param name="control">The model control of the session's engine, or <see langword="null"/> when the session has none: the engine's answer is then unknown.</param>
    /// <param name="modelVision">Tells whether the model with the given id reads images, or <see langword="null"/> when no model information is available.</param>
    /// <exception cref="ArgumentNullException"><paramref name="images"/> is null.</exception>
    public ImageInputBinding(ComposerAttachmentsModel images, IOmpModelControl? control, Func<string, VisionSupport>? modelVision)
    {
        ArgumentNullException.ThrowIfNull(images);

        _images = images;
        _control = control;
        _modelVision = modelVision;
        if (_control is not null)
        {
            _control.ModelChanged += OnModelChanged;
        }

        Push();
    }

    /// <summary>Stops following the control. The attachments keep the capabilities they have.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _control is not null)
        {
            _control.ModelChanged -= OnModelChanged;
        }
    }

    private void OnModelChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Push();
        }
        else
        {
            Dispatcher.UIThread.Post(Push);
        }
    }

    private void Push()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var vision = VisionSupport.Unknown;
        if (_control?.CurrentModelId is { Length: > 0 } modelId && _modelVision is not null)
        {
            vision = _modelVision(modelId);
        }

        _images.SetCapabilities(_control?.SupportsImages, vision);
    }
}
