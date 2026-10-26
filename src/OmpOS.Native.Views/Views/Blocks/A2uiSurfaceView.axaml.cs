using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using OmpOS.Presentation.Blocks;

namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// An A2UI surface. The presentation layer decides what each component becomes (<see cref="A2uiBlockMapping"/>); this view
/// only draws the render items. A button has no action on the client, and a remote image is a link that is not followed.
/// </summary>
internal partial class A2uiSurfaceView : UserControl
{
    /// <summary>Builds the view for one A2UI surface.</summary>
    /// <param name="block">The A2UI surface block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public A2uiSurfaceView(A2uiSurfaceBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        foreach (var item in A2uiBlockMapping.Map(block))
        {
            Components.Children.Add(ItemControl(item));
        }
    }

    private static Control ItemControl(A2uiRenderItem item) => item switch
    {
        A2uiHeadingItem heading => BlockControls.AsHeading(BlockControls.Text(heading.Text ?? string.Empty)),
        A2uiParagraphItem paragraph => BlockControls.Text(paragraph.Text ?? string.Empty),
        A2uiImageItem image => ImageControl(image),
        A2uiImageFallbackItem fallback => FallbackControl(fallback),
        A2uiButtonItem button => ButtonControl(button),
        A2uiListItem list => ListControl(list),
        A2uiUnsupportedItem unsupported => BlockControls.Disclosure(
            $"Unsupported content ({unsupported.Type})",
            BlockControls.Code(unsupported.Raw)),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item.GetType(), "No native view is mapped for this A2UI item."),
    };

    private static Control ImageControl(A2uiImageItem image)
    {
        var alt = A2uiSurfaceCatalog.ResolveImageAlt(image.Alt);
        var picture = DataImage.TryCreatePicture(image.Url.OriginalString);
        if (picture is null)
        {
            return BlockControls.Text(alt);
        }

        AutomationProperties.SetName(picture, alt);
        return picture;
    }

    private static TextBlock FallbackControl(A2uiImageFallbackItem fallback)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        text.Classes.Add("a2ui-image-link");
        var label = fallback.Text ?? string.Empty;
        if (fallback.Href is null)
        {
            text.Text = label;
            return text;
        }

        var inlines = BlockControls.InlinesOf(text);
        inlines.Add(new Run(label) { TextDecorations = BlockControls.Underline() });
        // The destination is not drawn as text; it is offered as the tooltip and as the accessible help text.
        ToolTip.SetTip(text, fallback.Href);
        AutomationProperties.SetHelpText(text, fallback.Href);

        return text;
    }

    private static Button ButtonControl(A2uiButtonItem button)
    {
        var label = button.Label ?? string.Empty;
        var control = new Button { Content = label };
        AutomationProperties.SetName(control, label.Length > 0 ? label : "Button");
        return control;
    }

    private static StackPanel ListControl(A2uiListItem list)
    {
        var panel = new StackPanel { Spacing = BlockTokens.Get<double>("OmpOS.Gap.Xs") };
        AutomationProperties.SetName(panel, "List");
        foreach (var item in list.Items)
        {
            panel.Children.Add(BlockControls.Row("•", BlockControls.Text(item, "a2ui-list-item")));
        }

        return panel;
    }
}
