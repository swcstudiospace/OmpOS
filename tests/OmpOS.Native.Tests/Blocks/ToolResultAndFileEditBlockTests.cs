using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Contracts.Ui;
using OmpOS.Native.Views.Blocks;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Blocks;

/// <summary>
/// Headless render checks for the tool-result and file-edit block views (contract 1.1.0). Every assertion reads what
/// the user sees. The views are built directly, except in the two tests that check the block-to-view switch of <c>ViewMapping</c>.
/// </summary>
public sealed class ToolResultAndFileEditBlockTests
{
    /// <summary>A tool result renders the tool's name, the reported status and the output text in monospace.</summary>
    [Fact]
    public async Task RendersToolResultBlock()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(new ToolResultBlockView(new ToolResultBlock
            {
                Id = "call-1-result",
                CallId = "call-1",
                ToolName = "ls",
                Status = "completed",
                Output = "a.txt\nb.txt",
            }));

            var texts = TextsOf(host.Window);
            texts.ShouldContain("Result: ls", StringComparer.Ordinal);
            texts.ShouldContain("completed", StringComparer.Ordinal);
            texts.ShouldContain("a.txt\nb.txt", StringComparer.Ordinal);
            var output = host.Window.GetVisualDescendants().OfType<TextBlock>().Single(static block => string.Equals(block.Text, "a.txt\nb.txt", StringComparison.Ordinal));
            output.FontFamily.ShouldBe(BlockTokens.Monospace);
            AutomationProperties.GetName(host.Window.GetVisualDescendants().OfType<ToolResultBlockView>().Single()).ShouldBe("Tool result ls, completed");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An edit of an existing file renders its path, the added and removed counts, the edit label and the unified diff rows: line number, sign, text (cursor-agents-window-parity.md 4.6, diff block anatomy).</summary>
    [Fact]
    public async Task RendersFileEditBlock()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(new FileEditBlockView(new FileEditBlock
            {
                Id = "call-1-edit-0",
                CallId = "call-1",
                Path = "src/a.cs",
                OldText = "int x = 1;",
                NewText = "int x = 2;",
            }));

            TextsOf(host.Window).ShouldBe(["src/a.cs", "+1", "-1", FileEditBlockView.EditedLabel, "1", "-", "int x = 1;", "1", "+", "int x = 2;"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A created file renders its new text alone as added rows, with a "-0" count and no removed rows (spec 4.6, diff block anatomy).</summary>
    [Fact]
    public async Task ACreatedFileRendersItsNewTextAlone()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var host = new Host(new FileEditBlockView(new FileEditBlock
            {
                Id = "call-2-edit-0",
                CallId = "call-2",
                Path = "src/new.cs",
                NewText = "class A;",
            }));

            TextsOf(host.Window).ShouldBe(["src/new.cs", "+1", "-0", FileEditBlockView.CreatedLabel, "1", "+", "class A;"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The block-to-view switch maps a tool result to its view, which renders the tool's name, status and output.</summary>
    [Fact]
    public async Task ViewMappingMapsAToolResultToItsView()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var view = ViewMapping.CreateBlock(new ToolResultBlock
            {
                Id = "call-3-result",
                CallId = "call-3",
                ToolName = "cat",
                Status = "failed",
                Output = "no such file",
            });

            view.ShouldBeOfType<ToolResultBlockView>();
            using var host = new Host(view);
            TextsOf(host.Window).ShouldBe(["Result: cat", "failed", "no such file"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The block-to-view switch maps a file edit to its view, which renders the path, the edit label and both texts.</summary>
    [Fact]
    public async Task ViewMappingMapsAFileEditToItsView()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var view = ViewMapping.CreateBlock(new FileEditBlock
            {
                Id = "call-4-edit-0",
                CallId = "call-4",
                Path = "src/b.cs",
                OldText = "a",
                NewText = "b",
            });

            view.ShouldBeOfType<FileEditBlockView>();
            using var host = new Host(view);
            TextsOf(host.Window).ShouldBe(["src/b.cs", "+1", "-1", FileEditBlockView.EditedLabel, "1", "-", "a", "1", "+", "b"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static List<string> TextsOf(Avalonia.Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text ?? string.Empty).ToList();

    /// <summary>A shown window that hosts one block view, closed when the test disposes it.</summary>
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
