using Autonogrammer.Native.Views.Blocks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Blocks;

/// <summary>A markdown image the view cannot draw falls back to readable text, and an inline data URI is never shown as that text.</summary>
public sealed class MarkdownAstBuilderTests
{
    [Fact]
    public async Task AnUndrawableDataUriImageWithoutAltTextShowsALabelNotTheUri()
    {
        await using var session = TestAppBuilder.StartSession();

        await session.Dispatch(() =>
        {
            var controls = MarkdownAstBuilder.Build("![](data:image/png;base64,AAAA)");

            RunTextOf(controls).ShouldBe("Image");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static string RunTextOf(IEnumerable<Control> controls) =>
        string.Concat(controls.OfType<TextBlock>().SelectMany(static block => block.Inlines.ShouldNotBeNull().OfType<Run>()).Select(static run => run.Text));
}
