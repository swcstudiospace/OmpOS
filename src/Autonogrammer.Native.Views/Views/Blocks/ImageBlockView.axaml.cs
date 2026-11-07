using Autonogrammer.Contracts.Ui;
using Autonogrammer.GenUi;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>
/// An image a tool returned. A kept, allowlisted payload is drawn through <see cref="DataImage.TryCreatePicture"/>,
/// which sniffs the header and caps the width. A placeholder, an oversize payload or a source outside the allowlist
/// is text only: nothing is decoded and no image control is created.
/// </summary>
internal partial class ImageBlockView : UserControl
{
    /// <summary>The label shown when the payload is not drawn.</summary>
    internal const string PlaceholderLabel = "Image omitted";

    /// <summary>Builds the view for one image block.</summary>
    /// <param name="block">The image block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public ImageBlockView(ImageBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        var alt = block.AltText;
        AltText.Text = CanDraw(block) ? alt : $"{alt} {PlaceholderLabel}";
        AutomationProperties.SetName(this, alt);

        if (CanDraw(block))
        {
            Picture.Content = DataImage.TryCreatePicture(block.DataUri!);
        }
    }

    private static bool CanDraw(ImageBlock block) =>
        !block.Omitted
        && block.DataUri is { Length: > 0 } uri
        && !ToolUpdateContent.RefusesImage(block.DataBase64)
        && A2uiSurfaceCatalog.IsAllowedImageSource(uri);
}
