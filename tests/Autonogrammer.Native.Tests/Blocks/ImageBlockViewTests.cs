using Autonogrammer.Contracts.Ui;
using Autonogrammer.Native.Views.Blocks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Blocks;

/// <summary>D-27: the native image view names itself, refuses a non-allowlisted source, and never decodes a 10 MiB payload.</summary>
public sealed class ImageBlockViewTests
{
    [Fact]
    public async Task AKeptPngHasAnAccessibleName()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(new ImageBlockView(Kept("image/png", "aGVsbG8=", "chart")));
            var view = host.Window.GetVisualDescendants().OfType<ImageBlockView>().Single();
            AutomationProperties.GetName(view).ShouldBe("chart");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task ANonAllowlistedSourceProducesNoImage()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(new ImageBlockView(Kept("image/svg+xml", "aGVsbG8=", "diagram")));
            host.Window.GetVisualDescendants().OfType<Image>().ShouldBeEmpty();
            AutomationProperties.GetName(host.Window.GetVisualDescendants().OfType<ImageBlockView>().Single()).ShouldBe("diagram");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task ATenMegabytePayloadIsAPlaceholderAndIsNotDecoded()
    {
        var data = new string('A', (ToolUpdateContent.MaxImageBytes / 3) * 4 + 4);
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(new ImageBlockView(new ImageBlock
            {
                Id = "call-1-image-0",
                CallId = "call-1",
                ToolName = "screenshot",
                MimeType = "image/png",
                DataBase64 = data,
                Description = "huge chart",
                ByteLength = ToolUpdateContent.MaxImageBytes,
                Omitted = false,
            }));

            host.Window.GetVisualDescendants().OfType<Image>().ShouldBeEmpty();
            AutomationProperties.GetName(host.Window.GetVisualDescendants().OfType<ImageBlockView>().Single()).ShouldBe("huge chart");
            TextsOf(host.Window).ShouldContain("huge chart Image omitted");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static ImageBlock Kept(string mime, string data, string description) => new()
    {
        Id = "call-1-image-0",
        CallId = "call-1",
        ToolName = "screenshot",
        MimeType = mime,
        DataBase64 = data,
        Description = description,
        ByteLength = 5,
        Omitted = false,
    };

    private static List<string> TextsOf(Avalonia.Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text ?? string.Empty).ToList();

    private sealed class Host : IDisposable
    {
        public Host(Control content)
        {
            Window = new Window { Content = content, Width = 640, Height = 480 };
            Window.Show();
            ShellHarness.Settle(Window);
        }

        public Window Window { get; }

        public void Dispose() => Window.Close();
    }
}
