using OmpOS.Contracts.Ui;
using OmpOS.GenUi;

namespace OmpOS.Presentation.Blocks;

/// <summary>
/// Projects an A2UI surface into the render items its view draws. The catalog rules live here, so the view
/// only binds and never decides what a component becomes.
/// </summary>
public static class A2uiBlockMapping
{
    /// <summary>Maps every component of the surface, in payload order, to the item the view renders.</summary>
    /// <param name="block">The A2UI surface block.</param>
    /// <returns>The render items. A supported component with nothing to show yields no item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public static IReadOnlyList<A2uiRenderItem> Map(A2uiSurfaceBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        var items = new List<A2uiRenderItem>();
        foreach (var component in A2uiSurfaceCatalog.GetComponents(block.Payload))
        {
            var item = ToItem(component);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static A2uiRenderItem? ToItem(SurfaceComponent component)
    {
        if (!component.Supported)
        {
            return new A2uiUnsupportedItem
            {
                Type = component.Type,
                Raw = component.Raw.GetRawText(),
            };
        }

        return component.Type switch
        {
            A2uiSurfaceCatalog.HeadingType => new A2uiHeadingItem { Text = component.Text },
            A2uiSurfaceCatalog.TextType => new A2uiParagraphItem { Text = component.Text },
            A2uiSurfaceCatalog.ImageType => ToImageItem(component),
            A2uiSurfaceCatalog.ButtonType => new A2uiButtonItem { Label = component.Label },
            A2uiSurfaceCatalog.ListType => new A2uiListItem { Items = component.Items },
            _ => null,
        };
    }

    private static A2uiRenderItem? ToImageItem(SurfaceComponent component)
    {
        if (component.ImageUrl is not null)
        {
            return new A2uiImageItem { Url = component.ImageUrl, Alt = component.Alt };
        }

        if (component.ImageSource is null)
        {
            return null;
        }

        return new A2uiImageFallbackItem { Href = component.ImageLinkHref, Text = component.ImageLinkText };
    }
}
