using System.Globalization;
using System.Text;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Rendering;
using Autonogrammer.Native.Views.Cowork.Artifacts;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The artifact panel view and its desktop surface: the labelled region and toolbar, the frame name and the skip order, the
/// diff markers, the CSV table, the image gate, the unknown-kind fallback, the Access review, and the surface wrapper's policy
/// and navigation rules. The surface tests load documents through <see cref="NativeWebViewArtifactSurface"/> on the headless platform.
/// </summary>
public sealed class ArtifactPanelViewTests
{
    private const string Origin = "https://cdn.example.com";
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Png1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    private static readonly string[] ToolbarNames = ["Diff", "Copy", "Export", "Edit", "Access", "Stop", "Close"];

    /// <summary>The labelled region, heading, toolbar names, target size, polite live region, frame name and the skip order.</summary>
    [Fact]
    public async Task TheChromeNamesTheRegionHeadingAndToolbarAndOrdersTheSkipLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
            await model.ShowAsync(Item(origins: [Origin]), ct).ConfigureAwait(true);
            var view = (ArtifactPanelView)ViewMapping.Create(model);
            var window = Show(view);
            try
            {
                AutomationProperties.GetName(view).ShouldBe("Artifact: Plan, version 3 of 5");
                view.HeadingLabel.Text.ShouldBe("Artifact: Plan, version 3 of 5");
                view.Toolbar.Children.OfType<Button>().Select(button => AutomationProperties.GetName(button) ?? string.Empty).ToArray().ShouldBe(ToolbarNames);

                var target = (double)Application.Current!.FindResource("Autonogrammer.Size.TargetMin")!;
                foreach (var command in Enum.GetValues<ArtifactCommand>())
                {
                    view.CommandButton(command).MinHeight.ShouldBe(target);
                    view.CommandButton(command).MinWidth.ShouldBe(target);
                }

                view.AccessBanner.IsVisible.ShouldBeTrue();
                AutomationProperties.GetLiveSetting(view.AnnouncementText).ShouldBe(AutomationLiveSetting.Polite);
                AutomationProperties.GetName(view.FrameArea).ShouldBe("Generated content: Plan (markdown), sandboxed");

                var order = window.GetVisualDescendants().ToList();
                order.IndexOf(view.SkipGeneratedContentLink).ShouldBeLessThan(order.IndexOf(view.FrameArea));
                order.IndexOf(view.FrameArea).ShouldBeLessThan(order.IndexOf(view.ReturnToChatButton));

                view.CommandButton(ArtifactCommand.Diff).IsEnabled.ShouldBeTrue();
                view.CommandButton(ArtifactCommand.Access).IsEnabled.ShouldBeTrue();
                view.CommandButton(ArtifactCommand.Stop).IsEnabled.ShouldBeTrue();
                view.CommandButton(ArtifactCommand.Close).IsEnabled.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }, ct);
    }

    /// <summary>A new version is announced politely and never changes the shown item.</summary>
    [Fact]
    public async Task ANewVersionIsAnnouncedAndDoesNotMoveTheShownVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
            await model.ShowAsync(Item(), ct).ConfigureAwait(true);
            var view = (ArtifactPanelView)ViewMapping.Create(model);
            var window = Show(view);
            try
            {
                await model.NotifyVersionCreatedAsync(Item(number: 4), ct).ConfigureAwait(true);
                view.AnnouncementText.Text.ShouldBe("Version 4 created by the agent");
                view.HeadingLabel.Text.ShouldBe("Artifact: Plan, version 3 of 5");
            }
            finally
            {
                window.Close();
            }
        }, ct);
    }

    /// <summary>An unknown kind shows its metadata and the no-display sentence, and only Export and Close are enabled.</summary>
    [Fact]
    public async Task AnUnknownKindShowsMetadataAndOnlyExport()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
            await model.ShowAsync(Item(kind: (ArtifactKind)99), ct).ConfigureAwait(true);
            var view = (ArtifactPanelView)ViewMapping.Create(model);
            var window = Show(view);
            try
            {
                AutomationProperties.GetName(view.FrameArea).ShouldBe("Generated content: Plan (kind 99), sandboxed");
                var texts = view.FrameContentHost.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();
                texts.ShouldContain("This version of Autonogrammer cannot display this kind. Export the version to keep it.", StringComparer.Ordinal);

                view.CommandButton(ArtifactCommand.Export).IsEnabled.ShouldBeTrue();
                view.CommandButton(ArtifactCommand.Close).IsEnabled.ShouldBeTrue();
                foreach (var command in new[] { ArtifactCommand.Diff, ArtifactCommand.Copy, ArtifactCommand.Edit, ArtifactCommand.Access, ArtifactCommand.Stop })
                {
                    view.CommandButton(command).IsEnabled.ShouldBeFalse();
                }
            }
            finally
            {
                window.Close();
            }
        }, ct);
    }

    /// <summary>The diff lines carry a sign and a word beside their text, so the change reads without colour.</summary>
    [Fact]
    public async Task TheDiffLinesCarryTheirSignAndWord()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
            await model.ShowAsync(Item(), ct).ConfigureAwait(true);
            var view = (ArtifactPanelView)ViewMapping.Create(model);
            var window = Show(view);
            try
            {
                var diff = new ArtifactDiffModel(
                    [
                        new ArtifactDiffLine(ArtifactDiffChange.Added, "new line"),
                        new ArtifactDiffLine(ArtifactDiffChange.Removed, "old line"),
                        new ArtifactDiffLine(ArtifactDiffChange.Unchanged, "same"),
                    ],
                    addedCount: 1,
                    removedCount: 1,
                    countsOnly: false,
                    metadataOnly: false);
                view.ShowDiff(diff);

                var lines = view.DiffPanelControl.Children.OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();
                lines.ShouldContain("Added 1 lines, removed 1.", StringComparer.Ordinal);
                lines.ShouldContain("+ Added: new line", StringComparer.Ordinal);
                lines.ShouldContain("- Removed: old line", StringComparer.Ordinal);
                lines.ShouldContain("  Unchanged: same", StringComparer.Ordinal);
                view.DiffPanelControl.IsVisible.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        }, ct);
    }

    /// <summary>A CSV version shows its caption, the header row as column headers, the formula mark, and the row-window notice when rows are capped.</summary>
    [Fact]
    public async Task ACsvVersionShowsCaptionHeadersFormulaMarksAndTheRowNotice()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new ArtifactPanelModel(new FakeArtifactSurface(), new ArtifactPanelOptions(AutoOpen: false));
            await model.ShowAsync(Item(kind: ArtifactKind.Csv), ct).ConfigureAwait(true);
            var view = (ArtifactPanelView)ViewMapping.Create(model);
            var window = Show(view);
            try
            {
                view.ShowContent(new ArtifactContent(ArtifactKind.Csv, "Stock", Encoding.UTF8.GetBytes("name,total\nwidget,=SUM(A1)\n")));
                var table = view.FrameContentHost.Content.ShouldBeOfType<ArtifactTableView>();
                table.CaptionText.ShouldBe("Stock: 1 rows, 2 columns");
                table.Table.Columns.Select(column => Convert.ToString(column.Header, CultureInfo.InvariantCulture) ?? string.Empty).ToArray().ShouldBe(["name", "total"]);
                table.NoticeText.ShouldBeEmpty();
                table.Table.ItemsSource.ShouldBeOfType<List<string[]>>().Single()[1].ShouldBe(ArtifactTableView.FormulaMark + "=SUM(A1)");

                var rows = new StringBuilder("h\n");
                for (var index = 0; index < 2100; index++)
                {
                    rows.Append(FormattableString.Invariant($"row{index},x\n"));
                }

                view.ShowContent(new ArtifactContent(ArtifactKind.Csv, "Big", Encoding.UTF8.GetBytes(rows.ToString())));
                var capped = view.FrameContentHost.Content.ShouldBeOfType<ArtifactTableView>();
                capped.NoticeText.ShouldBe(CsvReader.RowWindowNotice);
                capped.DataRowCount.ShouldBeGreaterThan(2000);
            }
            finally
            {
                window.Close();
            }
        }, ct);
    }

    /// <summary>An image is validated before it is decoded: refused bytes show a message, a valid PNG shows a picture.</summary>
    [Fact]
    public async Task AnImageIsValidatedBeforeItIsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var refused = new ArtifactImageView();
            refused.Show("Pixel", "image/png", new byte[] { 1, 2, 3 });
            refused.MessageText.ShouldContain("This image cannot be shown");
            refused.PictureControl.IsVisible.ShouldBeFalse();

            var accepted = new ArtifactImageView();
            accepted.Show("Pixel", "image/png", Convert.FromBase64String(Png1x1));
            accepted.PictureControl.IsVisible.ShouldBeTrue();
            accepted.PictureControl.Source.ShouldNotBeNull();
            accepted.MessageText.ShouldBeEmpty();
        }, ct);
    }

    /// <summary>The Access review lists the host in ASCII with its decoded form and a mixed-script mark, Deny has focus, Escape denies once, and a second answer raises nothing.</summary>
    [Fact]
    public async Task TheAccessReviewDeniesOnEscapeOnceAndMarksMixedScripts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var review = new ArtifactAccessReviewModel(
                new ArtifactVersionKey("artifact-1", 3, Sha),
                ["https://xn--pple-43d.com"],
                TimeProvider.System,
                TimeSpan.FromMinutes(1));
            var view = new ArtifactAccessReviewView { DataContext = review };
            var choices = new List<ArtifactAccessChoice>();
            view.DecisionMade += (_, e) => choices.Add(e.Decision.Choice);
            var window = Show(view);
            try
            {
                var sentences = view.Origins.Children.OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();
                sentences.ShouldHaveSingleItem().ShouldStartWith("Origin: https://xn--pple-43d.com");
                sentences[0].ShouldContain("Shown as:");
                sentences[0].ShouldContain("Mixed scripts");
                view.DenyChoice.IsFocused.ShouldBeTrue();

                view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, PhysicalKey = PhysicalKey.Escape, Source = view });
                choices.ShouldBe([ArtifactAccessChoice.Deny]);
                review.Decision.ShouldNotBeNull().Origins.ShouldBeEmpty();

                view.VersionChoice.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, view.VersionChoice));
                choices.ShouldBe([ArtifactAccessChoice.Deny]);
            }
            finally
            {
                window.Close();
            }
        }, ct);
    }

    /// <summary>A document without the policy meta element first is refused before any web view is created.</summary>
    [Fact]
    public async Task TheSurfaceRefusesADocumentWithoutThePolicyFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var surface = new NativeWebViewArtifactSurface(_ => ArtifactDocumentResult.Ready("<p>no policy</p>"));
            await surface.LoadAsync(new ArtifactSurfaceRequest("artifact-1", 3, Sha, ArtifactKind.Html, []), ct).ConfigureAwait(true);
            surface.State.ShouldBe(ArtifactSurfaceState.Unavailable);
            surface.Reason.ShouldBe(ArtifactSurfaceReason.DocumentRefused);
            surface.HasView.ShouldBeFalse();
        }, ct);
    }

    /// <summary>A refused Mermaid bundle reaches the surface as Unavailable with MermaidAssetMissing, and the source is what the panel shows.</summary>
    [Fact]
    public async Task AMissingMermaidBundleIsTheSurfaceReason()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var content = new ArtifactContent(ArtifactKind.Mermaid, "Flow", Encoding.UTF8.GetBytes("flowchart LR; A-->B"));
            var assets = new EmbeddedMermaidAssetProvider(typeof(object).Assembly);
            var surface = new NativeWebViewArtifactSurface(request => ArtifactDocumentBuilder.Build(content, request.ApprovedOrigins, assets));
            await surface.LoadAsync(new ArtifactSurfaceRequest("artifact-1", 3, Sha, ArtifactKind.Mermaid, []), ct).ConfigureAwait(true);
            surface.State.ShouldBe(ArtifactSurfaceState.Unavailable);
            surface.Reason.ShouldBe(ArtifactSurfaceReason.MermaidAssetMissing);
            surface.HasView.ShouldBeFalse();
        }, ct);
    }

    /// <summary>The first load is the host document and is allowed once; every later navigation is cancelled and reported with its target.</summary>
    [Fact]
    public async Task LaterNavigationsAreCancelledAndReportedAfterTheFirstLoad()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var content = new ArtifactContent(ArtifactKind.Html, "Page", Encoding.UTF8.GetBytes("<p>hello</p>"));
            var assets = new EmbeddedMermaidAssetProvider(typeof(object).Assembly);
            var surface = new NativeWebViewArtifactSurface(request => ArtifactDocumentBuilder.Build(content, request.ApprovedOrigins, assets));
            var blocked = new List<Uri>();
            surface.NavigationBlocked += (_, e) => blocked.Add(e.Url);

            await surface.LoadAsync(new ArtifactSurfaceRequest("artifact-1", 3, Sha, ArtifactKind.Html, []), ct).ConfigureAwait(true);
            surface.HasView.ShouldBeTrue();
            surface.CancelNavigation(NativeWebViewArtifactSurface.DocumentBase).ShouldBeFalse();
            surface.CancelNavigation(NativeWebViewArtifactSurface.DocumentBase).ShouldBeTrue();

            var evil = new Uri("https://evil.example/");
            surface.CancelNavigation(evil).ShouldBeTrue();
            blocked.ShouldBe([NativeWebViewArtifactSurface.DocumentBase, evil]);

            surface.Destroy();
            surface.HasView.ShouldBeFalse();
            surface.State.ShouldBe(ArtifactSurfaceState.Idle);
        }, ct);
    }

    /// <summary>The surface subscribes to no message event, and its environment turns devtools off and asks for a private store.</summary>
    [Fact]
    public void TheSurfaceHasNoMessagePathAndAPrivateStore()
    {
        var path = Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Views", "Cowork", "Artifacts", "NativeWebViewArtifactSurface.cs");
        var code = CodeLines(File.ReadAllText(path));
        code.ShouldNotContain("WebMessageReceived");
        code.ShouldNotContain("WebResourceRequested");
        code.ShouldContain("args.EnableDevTools = false;");
        code.ShouldContain("windows.IsInPrivateModeEnabled = true;");
        code.ShouldContain("apple.NonPersistentDataStore = true;");
        code.ShouldContain("gtk.EphemeralDataManager = true;");
    }

    /// <summary>An approved origin reaches connect-src and never script-src, and the policy is first in the document.</summary>
    [Fact]
    public void AnApprovedOriginReachesConnectButNeverScriptSource()
    {
        var content = new ArtifactContent(ArtifactKind.Html, "Page", Encoding.UTF8.GetBytes("<p>hi</p>"));
        var assets = new EmbeddedMermaidAssetProvider(typeof(object).Assembly);
        var document = ArtifactDocumentBuilder.Build(content, [Origin], assets).Document.ShouldNotBeNull();
        document.StartsWith(NativeWebViewArtifactSurface.PolicyMetaPrefix, StringComparison.Ordinal).ShouldBeTrue();
        var directives = document.Split(';');
        directives.Single(directive => directive.Contains("connect-src", StringComparison.Ordinal)).ShouldContain(Origin);
        directives.Single(directive => directive.TrimStart().StartsWith("script-src", StringComparison.Ordinal)).ShouldNotContain(Origin);
    }

    /// <summary>A sanitised SVG becomes a policy document, and an SVG the sanitiser refuses shows its source instead.</summary>
    [Fact]
    public void AnSvgIsSanitisedOrRefused()
    {
        var assets = new EmbeddedMermaidAssetProvider(typeof(object).Assembly);
        var clean = new ArtifactContent(ArtifactKind.Svg, "Shape", Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"1\" height=\"1\"/></svg>"));
        var accepted = ArtifactDocumentBuilder.Build(clean, [], assets);
        accepted.Reason.ShouldBe(ArtifactSurfaceReason.None);
        accepted.Document.ShouldNotBeNull().ShouldContain("<svg");

        var hostile = new ArtifactContent(ArtifactKind.Svg, "Bad", Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"));
        var stripped = ArtifactDocumentBuilder.Build(hostile, [], assets);
        stripped.Reason.ShouldBe(ArtifactSurfaceReason.None);
        stripped.Document.ShouldNotBeNull().ShouldNotContain("<script");

        var malformed = new ArtifactContent(ArtifactKind.Svg, "Broken", Encoding.UTF8.GetBytes("<svg><rect></svg>"));
        var refused = ArtifactDocumentBuilder.Build(malformed, [], assets);
        refused.Document.ShouldBeNull();
        refused.Reason.ShouldBe(ArtifactSurfaceReason.DocumentRefused);
    }

    private static string CodeLines(string source) =>
        string.Join('\n', source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static ArtifactPanelItem Item(ArtifactKind kind = ArtifactKind.Markdown, int number = 3, int versions = 5, IReadOnlyList<string>? origins = null) =>
        new("artifact-1", "Plan", kind, number, versions, Sha, origins ?? []);

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 900, Height = 1200 };
        window.Show();
        window.Activate();
        ShellHarness.Settle(window);
        return window;
    }
}
