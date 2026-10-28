using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Commands.Handlers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Commands.Handlers;

/// <summary>Verifies the handlers that work through host seams: copy, about, logs, quit, feedback, open, changes and saved workspaces.</summary>
public sealed class HostSeamSlashHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SlashActionRequest Typed(string typed) => new("x", string.Empty, typed);

    private sealed class Session : ISlashSessionContext
    {
        public string? SessionId { get; set; } = "sess-1";

        public string? LastRequestId { get; set; } = "req-9";

        public string? LastResponseText { get; set; } = "the answer";

        public string? LastUserMessageText { get; set; } = "the question";
    }

    private sealed class Clipboard : IClipboardWriter
    {
        public List<string> Texts { get; } = [];

        public bool Accepts { get; set; } = true;

        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken)
        {
            Texts.Add(text);
            return Task.FromResult(Accepts);
        }
    }

    [Fact]
    public async Task CopyCopiesTheLastResponseOrWithTheWordUserTheLastUserMessage()
    {
        var clipboard = new Clipboard();
        var handler = new CopySlashHandler(HandlerActionIds.ChatCopy, new Session(), clipboard);

        var response = await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        var user = await handler.RunAsync(Typed("User"), Token).ConfigureAwait(true);

        clipboard.Texts.ShouldBe(["the answer", "the question"]);
        response.Message.ShouldBe("Copied the last response to the clipboard.");
        user.Message.ShouldBe("Copied your last message to the clipboard.");
    }

    [Fact]
    public async Task TheIdCommandsCopyTheirIdsAndSayWhenThereIsNone()
    {
        var clipboard = new Clipboard();
        var session = new Session();
        var conversation = new CopySlashHandler(HandlerActionIds.ChatCopyConversationId, session, clipboard);
        var request = new CopySlashHandler(HandlerActionIds.ChatCopyRequestId, session, clipboard);

        (await conversation.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Outcome.ShouldBe(SlashActionOutcome.Completed);
        (await request.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Outcome.ShouldBe(SlashActionOutcome.Completed);
        session.LastRequestId = null;
        session.SessionId = " ";
        session.LastResponseText = null;
        var noRequest = await request.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        var noSession = await conversation.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        var noResponse = await new CopySlashHandler(HandlerActionIds.ChatCopy, session, clipboard).RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);

        clipboard.Texts.ShouldBe(["sess-1", "req-9"]);
        noRequest.Message.ShouldBe(HandlerCopy.NoRequestId);
        noResponse.Message.ShouldBe(HandlerCopy.NothingToCopy);
        noSession.Message.ShouldBe(HandlerCopy.NoSessionId);
    }

    [Fact]
    public async Task AClipboardThatRefusesTheTextIsAFailedResultAndAnUnknownActionIsRefused()
    {
        var handler = new CopySlashHandler(HandlerActionIds.ChatCopy, new Session(), new Clipboard { Accepts = false });

        var result = await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Failed);
        result.Message.ShouldBe(HandlerCopy.ClipboardRefused());
        Should.Throw<ArgumentException>(() => new CopySlashHandler("chat.other", new Session(), new Clipboard()));
    }

    private sealed class About : IAboutLauncher
    {
        public int Shown { get; private set; }

        public Task ShowAsync(CancellationToken cancellationToken)
        {
            Shown++;
            return Task.CompletedTask;
        }
    }

    private sealed class Lifetime : IApplicationLifetime
    {
        public int Requests { get; private set; }

        public Task RequestShutdownAsync(CancellationToken cancellationToken)
        {
            Requests++;
            return Task.CompletedTask;
        }
    }

    private sealed class Folders : IFolderOpener
    {
        public List<string> Opened { get; } = [];

        public bool Works { get; set; } = true;

        public Task<bool> OpenAsync(string folder, CancellationToken cancellationToken)
        {
            Opened.Add(folder);
            return Task.FromResult(Works);
        }
    }

    [Fact]
    public async Task AboutAndQuitCallTheirSeamsOnceAndLogsOpensTheFolderOrSaysItFailed()
    {
        var about = new About();
        var lifetime = new Lifetime();
        var folders = new Folders();

        (await new AboutSlashHandler(about).RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Outcome.ShouldBe(SlashActionOutcome.Completed);
        (await new QuitSlashHandler(lifetime).RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Message.ShouldBe(HandlerCopy.Quitting);
        var logs = new LogsSlashHandler(folders, () => "/state/logs");
        var opened = await logs.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        folders.Works = false;
        var failed = await logs.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);

        about.Shown.ShouldBe(1);
        lifetime.Requests.ShouldBe(1);
        folders.Opened.ShouldBe(["/state/logs", "/state/logs"]);
        opened.Message.ShouldBe("Opened the log folder: /state/logs");
        failed.Outcome.ShouldBe(SlashActionOutcome.Failed);
    }

    private sealed class Urls : IUrlLauncher
    {
        public List<Uri> Opened { get; } = [];

        public bool Works { get; set; } = true;

        public Task<bool> OpenAsync(Uri url, CancellationToken cancellationToken)
        {
            Opened.Add(url);
            return Task.FromResult(Works);
        }
    }

    [Fact]
    public async Task FeedbackOpensTheIssueFormWithTheTypedTextAsAnEscapedAndShortenedTitle()
    {
        var urls = new Urls();
        var handler = new FeedbackSlashHandler(urls);

        await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        await handler.RunAsync(Typed("Crash & burn?\nplease"), Token).ConfigureAwait(true);
        await handler.RunAsync(Typed(new string('a', 500)), Token).ConfigureAwait(true);

        urls.Opened[0].AbsoluteUri.ShouldBe("https://github.com/swcstudiospace/OmpOS/issues/new");
        urls.Opened[1].Query.ShouldBe("?title=Crash%20%26%20burn%3F%20please");
        urls.Opened[2].Query.Length.ShouldBe("?title=".Length + 200);
    }

    [Fact]
    public async Task FeedbackOnlyOpensHttpsAddressesAndReportsABrowserThatDidNotStart()
    {
        Should.Throw<ArgumentException>(() => new FeedbackSlashHandler(new Urls(), "http://example.invalid/issues"));
        Should.Throw<ArgumentException>(() => new FeedbackSlashHandler(new Urls(), "file:///etc/passwd"));
        var handler = new FeedbackSlashHandler(new Urls { Works = false }, "https://example.invalid/issues/new");

        var result = await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Failed);
        result.Message.ShouldContain("https://example.invalid/issues/new", Case.Sensitive);
    }

    private sealed class Editor : IEditorOpener
    {
        public List<(string Path, int? Line)> Opened { get; } = [];

        public bool Works { get; set; } = true;

        public Task<bool> OpenAsync(string workspacePath, int? line, CancellationToken cancellationToken)
        {
            Opened.Add((workspacePath, line));
            return Task.FromResult(Works);
        }
    }

    [Fact]
    public async Task OpenNormalisesThePathReadsTheLineAndNeverAsksTheEditorForAPathOutsideTheWorkspace()
    {
        var editor = new Editor();
        var handler = new OpenFileSlashHandler(editor);

        var plain = await handler.RunAsync(Typed("./src/../src/Program.cs"), Token).ConfigureAwait(true);
        var withLine = await handler.RunAsync(Typed("src\\a.cs:42"), Token).ConfigureAwait(true);
        var zero = await handler.RunAsync(Typed("notes.txt:0"), Token).ConfigureAwait(true);
        var outside = await handler.RunAsync(Typed("../secret.txt"), Token).ConfigureAwait(true);
        var absolute = await handler.RunAsync(Typed("/etc/passwd"), Token).ConfigureAwait(true);
        var root = await handler.RunAsync(Typed("."), Token).ConfigureAwait(true);
        var none = await handler.RunAsync(Typed("  "), Token).ConfigureAwait(true);

        editor.Opened.ShouldBe([("src/Program.cs", null), ("src/a.cs", 42), ("notes.txt:0", null)]);
        plain.Message.ShouldBe("Opened src/Program.cs.");
        withLine.Message.ShouldBe("Opened src/a.cs at line 42.");
        zero.Outcome.ShouldBe(SlashActionOutcome.Completed);
        outside.Message.ShouldBe(HandlerCopy.OpenOutside);
        absolute.Message.ShouldBe(HandlerCopy.OpenOutside);
        root.Message.ShouldBe(HandlerCopy.OpenNeedsPath);
        none.Message.ShouldBe(HandlerCopy.OpenNeedsPath);
    }

    [Fact]
    public async Task OpenReportsAFileTheEditorCouldNotOpen()
    {
        var handler = new OpenFileSlashHandler(new Editor { Works = false });

        var result = await handler.RunAsync(Typed("missing.txt"), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Failed);
        result.Message.ShouldBe("Could not open missing.txt. Check that the file exists.");
    }

    private sealed class Bar : IChangesBarOpener
    {
        public bool HasChanges { get; set; }

        public int Shown { get; private set; }

        public void Show() => Shown++;
    }

    [Fact]
    public async Task ChangesShowsTheBarOnlyWhenThereAreChangesAndNeedsAChat()
    {
        var bar = new Bar();
        var handler = new ChangesSlashHandler(() => bar);

        var none = await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        bar.HasChanges = true;
        var shown = await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);

        none.Message.ShouldBe(HandlerCopy.NoChanges);
        shown.Message.ShouldBe(HandlerCopy.ChangesOpened);
        bar.Shown.ShouldBe(1);
        new ChangesSlashHandler(() => null).CanRun.ShouldBeFalse();
    }

    private sealed class Sets : IWorkspaceSetsBackend
    {
        public List<string> Names { get; } = ["backend", "docs"];

        public List<string> Saved { get; } = [];

        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([.. Names]);

        public Task SaveAsync(string name, CancellationToken cancellationToken)
        {
            Saved.Add(name);
            return Task.CompletedTask;
        }

        public Task<bool> LoadAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Names.Contains(name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task SaveWorkspaceNeedsAValidNameAndLoadWorkspaceListsOpensOrNamesWhatExists()
    {
        var sets = new Sets();
        var save = new WorkspaceSetSlashHandler(HandlerActionIds.WorkspaceSave, sets);
        var load = new WorkspaceSetSlashHandler(HandlerActionIds.WorkspaceLoad, sets);

        (await save.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Message.ShouldBe(HandlerCopy.WorkspaceNeedsName);
        (await save.RunAsync(Typed(new string('n', 81)), Token).ConfigureAwait(true)).Message.ShouldBe(HandlerCopy.WorkspaceBadName);
        (await save.RunAsync(Typed("bad\u0007name"), Token).ConfigureAwait(true)).Message.ShouldBe(HandlerCopy.WorkspaceBadName);
        (await save.RunAsync(Typed("  frontend "), Token).ConfigureAwait(true)).Message.ShouldBe("Saved the open folders as the workspace frontend.");
        var list = await load.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        var loaded = await load.RunAsync(Typed("docs"), Token).ConfigureAwait(true);
        var unknown = await load.RunAsync(Typed("nope"), Token).ConfigureAwait(true);

        sets.Saved.ShouldBe(["frontend"]);
        list.Message.ShouldBe("Saved workspaces: backend, docs. Type /load-workspace <name> to open one.");
        loaded.Message.ShouldBe("Opened the workspace docs.");
        unknown.Outcome.ShouldBe(SlashActionOutcome.Failed);
        unknown.Message.ShouldBe("There is no saved workspace named nope. Saved: backend, docs.");
        Should.Throw<ArgumentException>(() => new WorkspaceSetSlashHandler("workspace.other", sets));
    }
}
